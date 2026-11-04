// Точка входа страницы: тексты ответов из API → данные пользователя → логика базы знаний.
// Панель откликов (app.js) живёт отдельно и грузится параллельно.
import { renderContent } from './render.js';
import { loadStore } from './store.js';
import { startKnowledge } from './knowledge.js';

try {
  await renderContent();
  startKnowledge(await loadStore());
  // браузер переходит по якорю (#q7.1) до того, как тексты пришли из API, — прокручиваем сами
  if (location.hash) document.getElementById(decodeURIComponent(location.hash.slice(1)))?.scrollIntoView({ block: 'start' });
} catch (err) {
  document.querySelector('main').innerHTML = `<div class="jobs-error">Не удалось загрузить базу знаний: ${String(err.message).replace(/</g, '&lt;')}</div>`;
  console.error(err);
}
