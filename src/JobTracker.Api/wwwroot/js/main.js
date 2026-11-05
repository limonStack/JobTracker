// Точка входа: работает и без сервера. Локальные данные (IndexedDB) → тексты ответов → синхронизация
// с сервером, если он доступен → логика базы знаний и панель откликов.
import { openStorage, memoryStorage } from './db.js';
import { createSync } from './sync.js';
import { renderContent } from './render.js';
import { loadStore } from './store.js';
import { startKnowledge } from './knowledge.js';
import { startJobs } from '../app.js';

const esc = s => String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;');

// офлайн-оболочка: страница, стили и скрипты открываются из кэша, когда сервер выключен
if ('serviceWorker' in navigator) navigator.serviceWorker.register('/sw.js').catch(err => console.warn('service worker', err));

// индикатор в шапке: онлайн / офлайн и сколько правок ждут отправки; клик — синхронизировать сейчас
function statusBadge(sync) {
  const el = document.createElement('button');
  el.type = 'button';
  el.id = 'sync-status';
  el.className = 'sync-status';
  document.querySelector('#side .brand > span')?.after(el);
  const show = ({ online, pending, failed }) => {
    const wait = pending ? ` · ${pending}` : '';           // сколько правок ждут — подробно в подсказке
    el.className = 'sync-status' + (failed.length ? ' failed' : online === false ? ' offline' : '');
    el.textContent = failed.length ? `⚠ ${failed.length}` : online === false ? `○ офлайн${wait}` : `● онлайн${wait}`;
    el.title = failed.length
      ? `Не сохранилось — сервер отклонил изменения (клик — скрыть):\n${failed.join('\n')}`
      : online === false
        ? `Сервер недоступен: правки сохраняются в браузере и уйдут, когда он появится${pending ? ` (ждут: ${pending})` : ''}. Клик — проверить сейчас`
        : 'Данные синхронизированы с сервером. Клик — синхронизировать сейчас';
  };
  el.addEventListener('click', () => sync.status().failed.length ? sync.clearFailed() : sync.syncNow());
  sync.onChange(show);
  show(sync.status());
}

try {
  // IndexedDB бывает недоступен (приватный режим некоторых браузеров) — тогда без офлайна, но страница работает
  const storage = await openStorage().catch(() => memoryStorage());
  const sync = createSync({ storage });
  await sync.init();
  statusBadge(sync);

  await renderContent(storage);
  await sync.syncNow();      // сервер выключен — не страшно, работаем со снимком из браузера
  if (!sync.state)
    throw new Error('Сервер недоступен, а данных в браузере ещё нет — откройте страницу один раз при запущенном сервере');

  startKnowledge(loadStore(sync));
  startJobs(sync);
  sync.start();
  // браузер переходит по якорю (#q7.1) до того, как тексты построены, — прокручиваем сами
  if (location.hash) document.getElementById(decodeURIComponent(location.hash.slice(1)))?.scrollIntoView({ block: 'start' });
} catch (err) {
  document.querySelector('main').innerHTML = `<div class="jobs-error">Не удалось загрузить базу знаний: ${esc(err.message)}</div>`;
  console.error(err);
}
