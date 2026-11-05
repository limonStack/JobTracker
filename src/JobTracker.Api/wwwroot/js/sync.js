// Офлайн-синхронизация. Главная копия данных — в базе на сервере; в браузере — снимок (GET /api/sync)
// и очередь изменений. Правка сразу применяется к снимку и попадает в очередь, поэтому страница
// работает без сервера. Когда сервер доступен: очередь уходит по порядку, затем приходит свежий снимок.
// Запросы очереди идемпотентны (PUT/DELETE с id от клиента), поэтому повтор после обрыва связи безопасен.

/** UUID v7: время + случайность — id новой записи, выданный в браузере (сервер примет его как есть). */
export function uuidv7() {
  const b = crypto.getRandomValues(new Uint8Array(16));
  let ms = Date.now();
  for (let i = 5; i >= 0; i--) { b[i] = ms % 256; ms = Math.floor(ms / 256); }
  b[6] = (b[6] & 0x0f) | 0x70;
  b[8] = (b[8] & 0x3f) | 0x80;
  const h = [...b].map(x => x.toString(16).padStart(2, '0')).join('');
  return `${h.slice(0, 8)}-${h.slice(8, 12)}-${h.slice(12, 16)}-${h.slice(16, 20)}-${h.slice(20)}`;
}

// ответ, после которого повторять бессмысленно (кроме 408/429 — это «подождите»)
const isClientError = s => s >= 400 && s < 500 && s !== 408 && s !== 429;

export function createSync({ storage, fetchImpl = (...a) => fetch(...a), intervalMs = 60_000 }) {
  let state = null;              // снимок данных пользователя (формат GET /api/sync)
  let pending = 0;               // изменений в очереди
  let online = null;             // null — ещё не пробовали связаться
  let failed = [];               // изменения, которые сервер отклонил (400): показываем пользователю
  let flushing = null;
  const listeners = new Set();
  const replacedListeners = new Set();     // пришёл и применён свежий снимок с сервера
  const emit = () => listeners.forEach(f => f(status()));
  const status = () => ({ online, pending, failed: [...failed] });

  async function send(op) {
    return fetchImpl(op.url, {
      method: op.method,
      headers: op.body === undefined ? {} : { 'Content-Type': 'application/json' },
      body: op.body === undefined ? undefined : JSON.stringify(op.body),
    });
  }

  /** Отправить очередь по порядку. True — очередь пуста; false — сервер недоступен, остаток ждёт. */
  function flush() {
    flushing ??= (async () => {
      try {
        // перечитываем очередь, пока она не опустеет: правки, добавленные во время отправки, уйдут сразу
        for (let ops = await storage.outboxAll(); ops.length > 0; ops = await storage.outboxAll())
        for (const op of ops) {
          let res;
          try {
            res = await send(op);
          } catch {
            online = false;      // сеть или сервер выключен
            return false;
          }
          online = true;
          if (res.ok || (op.method === 'DELETE' && res.status === 404)) {
            // удалить уже удалённое — тоже успех
          } else if (isClientError(res.status)) {
            let detail = `${res.status}`;
            try { const p = await res.json(); detail = p.detail || p.title || detail; } catch { /* не JSON */ }
            failed.push(`${op.label || op.url}: ${detail}`);
          } else {
            return false;        // 5xx — попробуем позже, порядок важнее
          }
          await storage.outboxDelete(op.seq);
          pending = Math.max(0, pending - 1);
          emit();
        }
        return true;
      } finally {
        flushing = null;
        emit();
      }
    })();
    return flushing;
  }

  /**
   * Свежий снимок с сервера. Применяется, только если очередь пуста: иначе он ещё не знает о локальных
   * правках и «откатил» бы их. Снимок меняется на месте — модули страницы держат ссылку на state.
   */
  async function pull() {
    let res;
    try {
      res = await fetchImpl('/api/sync');
    } catch {
      online = false;
      emit();
      return false;
    }
    online = true;
    if (!res.ok) { emit(); return false; }
    const data = await res.json();
    if ((await storage.outboxAll()).length > 0) return false;

    if (state === null) state = data;
    else { for (const k of Object.keys(state)) delete state[k]; Object.assign(state, data); }
    await storage.set('snapshot', state);
    emit();
    replacedListeners.forEach(f => f());
    return true;
  }

  return {
    get state() { return state; },
    status,
    onChange: f => { listeners.add(f); return () => listeners.delete(f); },
    onReplaced: f => { replacedListeners.add(f); return () => replacedListeners.delete(f); },
    clearFailed() { failed = []; emit(); },

    /** Снимок и размер очереди из браузера — до любого обращения к сети. */
    async init() {
      state = (await storage.get('snapshot')) ?? null;
      pending = (await storage.outboxAll()).length;
      emit();
    },

    /** Изменение: снимок уже поправлен вызывающим кодом; здесь — в очередь и сохранить снимок. */
    async enqueue(method, url, body, label) {
      await storage.outboxAdd({ method, url, body, label, at: new Date().toISOString() });
      pending++;
      await storage.set('snapshot', state);
      emit();
      flush();                   // сразу пробуем отправить; не вышло — останется в очереди
    },

    /** Сохранить снимок без запроса к серверу (например, после чисто локальных правок). */
    saveLocal: () => storage.set('snapshot', state),

    async syncNow() {
      // отправка, начатая раньше (например, сразу после правки), могла застать сервер выключенным —
      // дожидаемся её и пробуем заново, а не возвращаем её устаревший результат
      if (flushing) await flushing;
      if (await flush()) return pull();
      return false;
    },

    /** Повторять синхронизацию: при появлении сети, при возврате на вкладку и раз в минуту. */
    start() {
      const again = () => this.syncNow();
      addEventListener('online', again);
      document.addEventListener('visibilitychange', () => { if (!document.hidden) again(); });
      setInterval(again, intervalMs);
    },
  };
}
