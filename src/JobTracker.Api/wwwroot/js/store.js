// Данные пользователя для базы знаний: загружаются из API, изменения уходят обратно на сервер.
// Старый код (knowledge.js) работает с данными в прежнем формате localStorage — здесь он переводится
// в запросы API. Списки (идеи, выделения, отметки) сравниваются с последним сохранённым состоянием,
// и на сервер уходит только разница. Запросы идут по очереди: правка только что созданной идеи
// дождётся, пока сервер выдаст ей id.

export async function api(method, url, body) {
  const r = await fetch(url, {
    method,
    headers: body === undefined ? {} : { 'Content-Type': 'application/json' },
    body: body === undefined ? undefined : JSON.stringify(body),
  });
  if (!r.ok) {
    let text = `${r.status} ${r.statusText}`;
    try { const p = await r.json(); text = p.detail || p.title || text; } catch { /* не JSON */ }
    throw new Error(text);
  }
  return r.status === 204 ? null : r.json();
}

// сбой сохранения не должен пройти незаметно: данные теперь на сервере, а не в браузере
function showError(message) {
  let box = document.getElementById('sync-error');
  if (!box) {
    box = document.createElement('div');
    box.id = 'sync-error';
    box.className = 'sync-error';
    box.addEventListener('click', () => box.remove());
    document.body.append(box);
  }
  box.textContent = `⚠ Не сохранилось: ${message}. Обновите страницу (клик — скрыть)`;
}

let queue = Promise.resolve();
const enqueue = job => (queue = queue.then(job).catch(err => showError(err.message)));

const ideaFrom = x => ({ id: x.id, d: x.createdOn, t: x.title, n: x.note, p: x.project, q: x.question, s: x.status, done: x.doneOn || '' });
const ideaKey = x => JSON.stringify([x.t, x.n, x.p, x.q]);
const hlFrom = h => ({ id: h.id, sec: h.section, text: h.text, prefix: h.prefix, suffix: h.suffix, start: h.start, end: h.start + h.text.length, color: h.color });
const hlBody = h => ({ section: h.sec, text: h.text, prefix: h.prefix, suffix: h.suffix, start: h.start, color: h.color });
const hlKey = h => JSON.stringify(hlBody(h));

export async function loadStore() {
  const [study, ideaList, highlightList] = await Promise.all([
    api('GET', '/api/study'), api('GET', '/api/ideas'), api('GET', '/api/highlights'),
  ]);

  const settings = { ...study.settings };
  // «сегодня» сервер считает по часовому поясу пользователя — сообщаем ему пояс браузера
  const zone = Intl.DateTimeFormat().resolvedOptions().timeZone;
  if (zone && settings.timeZone !== zone) { settings.timeZone = zone; enqueue(() => api('PUT', '/api/settings', settings)); }

  // ---- отметки «изучено»: Set + { код: день }, как в старом сайте ----
  const done = new Set(study.marks.map(m => m.code));
  const doneAt = Object.fromEntries(study.marks.filter(m => m.day).map(m => [m.code, m.day]));
  let savedMarks = new Map(study.marks.map(m => [m.code, m.day || null]));

  // ---- треки и повторение ----
  const activities = { workout: new Set(), ai: new Set() };
  for (const a of study.activities) activities[a.kind]?.add(a.day);
  const review = {};
  for (const r of study.reviews) (review[r.day] ||= []).push(r.code);

  // ---- идеи и выделения: временный id новой записи → id на сервере ----
  const serverId = new Map();
  const idOf = id => serverId.get(id) || id;
  const ideas = ideaList.map(ideaFrom);
  let savedIdeas = new Map(ideas.map(x => [x.id, { key: ideaKey(x), s: x.s }]));
  const highlights = highlightList.map(hlFrom);
  let savedHighlights = new Map(highlights.map(h => [h.id, hlKey(h)]));

  return {
    study, settings, done, doneAt, review, ideas, highlights,

    activityDays: kind => activities[kind],

    syncMarks(currentDone, currentAt) {
      const next = new Map([...currentDone].map(c => [c, currentAt[c] || null]));
      for (const [code, day] of next)
        if (!savedMarks.has(code) || savedMarks.get(code) !== day)
          enqueue(() => api('PUT', `/api/study/marks/${encodeURIComponent(code)}`, { day }));
      for (const code of savedMarks.keys())
        if (!next.has(code)) enqueue(() => api('DELETE', `/api/study/marks/${encodeURIComponent(code)}`));
      savedMarks = next;
    },

    setActivity: (kind, day, on) => enqueue(() => api(on ? 'PUT' : 'DELETE', `/api/study/activities/${kind}/${day}`)),

    setReviewed: (day, code, on) => enqueue(() => api(on ? 'PUT' : 'DELETE', `/api/study/reviews/${day}/${encodeURIComponent(code)}`)),

    saveSettings(patch) {
      Object.assign(settings, patch);
      const body = { ...settings };
      enqueue(() => api('PUT', '/api/settings', body));
    },

    syncIdeas(current) {
      const next = new Map();
      for (const x of current) {
        const key = ideaKey(x), was = savedIdeas.get(x.id), id = x.id;
        // тело — на момент изменения: запрос выполнится позже, а объект к тому времени могут поправить снова
        const fields = { title: x.t, note: x.n, project: x.p, question: x.q }, status = x.s;
        next.set(id, { key, s: status });
        if (!was) {
          enqueue(async () => {
            const created = await api('POST', '/api/ideas', { title: fields.title });
            serverId.set(id, created.id);
            if (fields.note || fields.project || fields.question) await api('PUT', `/api/ideas/${created.id}`, fields);
            if (status !== 'new') await api('PUT', `/api/ideas/${created.id}/status`, { status });
          });
          continue;
        }
        if (was.key !== key) enqueue(() => api('PUT', `/api/ideas/${idOf(id)}`, fields));
        if (was.s !== status) enqueue(() => api('PUT', `/api/ideas/${idOf(id)}/status`, { status }));
      }
      for (const id of savedIdeas.keys())
        if (!next.has(id)) enqueue(() => api('DELETE', `/api/ideas/${idOf(id)}`));
      savedIdeas = next;
    },

    syncHighlights(current) {
      const next = new Map();
      for (const h of current) {
        const key = hlKey(h), was = savedHighlights.get(h.id), id = h.id, body = hlBody(h);
        next.set(id, key);
        if (was === undefined)
          enqueue(async () => serverId.set(id, (await api('POST', '/api/highlights', body)).id));
        else if (was !== key)
          enqueue(() => api('PUT', `/api/highlights/${idOf(id)}`, body));
      }
      for (const id of savedHighlights.keys())
        if (!next.has(id)) enqueue(() => api('DELETE', `/api/highlights/${idOf(id)}`));
      savedHighlights = next;
    },
  };
}
