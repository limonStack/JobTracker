// Дела на сегодня: панель со списком (кнопка 📝 в шапке меню).
// Данные — в локальном снимке (sync.js): правка сразу в снимок, на сервер — через очередь, работает и офлайн.
// Невыполненное дело не теряется: пока его не отметили, оно показывается и в следующие дни с пометкой,
// с какого дня тянется. День записи при этом не меняется — перенос только в показе (todayList).

const MONTHS = ['янв', 'фев', 'мар', 'апр', 'мая', 'июн', 'июл', 'авг', 'сен', 'окт', 'ноя', 'дек'];
const esc = s => String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

/** Ключ дня «ГГГГ-ММ-ДД» по местному времени. */
export const dayKey = (d = new Date()) =>
  `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;

/** «вчера» / «с 15 ноя» — откуда перенесено невыполненное дело. */
export function carriedLabel(plannedOn, today) {
  const days = Math.round((Date.parse(today) - Date.parse(plannedOn)) / 86_400_000);
  return days === 1 ? 'со вчера' : `с ${+plannedOn.slice(8)} ${MONTHS[+plannedOn.slice(5, 7) - 1]}`;
}

/**
 * Список на день: невыполненные, записанные на этот день или раньше (перенесённые), и сделанные в этот день.
 * Сначала несделанные — старые сверху, потом сделанные; внутри — в порядке добавления (id — UUID v7, растёт со временем).
 */
export function todayList(todos, today) {
  const byOrder = (a, b) => a.plannedOn.localeCompare(b.plannedOn) || a.id.localeCompare(b.id);
  const open = todos.filter(t => !t.doneOn && t.plannedOn <= today).sort(byOrder);
  const done = todos.filter(t => t.doneOn === today).sort(byOrder);
  return [...open, ...done].map(t => ({ ...t, carried: !t.doneOn && t.plannedOn < today }));
}

export function startTodos(sync, newId) {
  const panel = document.getElementById('todos');
  const btn = document.getElementById('todos-toggle');
  // снимок, сохранённый до появления дел, — без списка
  const todos = () => (sync.state.todos ||= []);
  const find = id => todos().find(t => t.id === id);

  function render() {
    if (panel.hidden) return;
    const today = dayKey();
    const list = todayList(todos(), today);
    const rows = list.map(t =>
      `<li class="todo${t.doneOn ? ' done' : ''}" data-id="${t.id}">` +
        `<input type="checkbox" data-act="done" aria-label="Сделано"${t.doneOn ? ' checked' : ''}>` +
        `<input class="todo-title" data-act="title" value="${esc(t.title)}" aria-label="Дело" maxlength="300">` +
        (t.carried ? `<span class="todo-carried" title="Записано ${t.plannedOn.split('-').reverse().join('.')}">${carriedLabel(t.plannedOn, today)}</span>` : '<span></span>') +
        `<button type="button" class="todo-del" data-act="del" title="Удалить" aria-label="Удалить">✕</button>` +
      `</li>`).join('');
    const focused = document.activeElement?.closest('#todos') ? document.activeElement.name : null;
    panel.innerHTML =
      `<div class="jobs-head"><span class="jobs-title">📝 Дела на сегодня</span>` +
        `<span class="job-empty">${+today.slice(8)} ${MONTHS[+today.slice(5, 7) - 1]}</span>` +
        `<button type="button" class="cal-close" data-act="close" title="Закрыть" aria-label="Закрыть">✕</button></div>` +
      `<form class="idea-add" data-act="add"><input name="t" placeholder="Новое дело — Enter, чтобы добавить" required autocomplete="off" maxlength="300">` +
        `<button type="submit" class="jobs-btn primary">Добавить</button></form>` +
      (rows ? `<ul class="todo-list">${rows}</ul>`
        : `<div class="job-empty">На сегодня дел нет. Что не успеете — останется в списке и завтра.</div>`);
    if (focused === 't') panel.querySelector('.idea-add input').focus();
  }

  // правка — сразу в снимок и в очередь на сервер (запросы идемпотентны: id и день — от браузера)
  const commit = (method, url, body, label) => { sync.enqueue(method, url, body, label); render(); };

  function setOpen(open) {
    // панели стоят на одном месте — открыта одна
    if (open) for (const id of ['jobs', 'ideas'])
      if (!document.getElementById(id).hidden) document.getElementById(id + '-toggle').click();
    panel.hidden = !open;
    btn.setAttribute('aria-pressed', open);
    render();
    if (open) panel.querySelector('.idea-add input').focus();
  }

  btn.addEventListener('click', () => setOpen(panel.hidden));
  // открыли отклики или идеи — дела закрываются
  for (const id of ['jobs-toggle', 'ideas-toggle'])
    document.getElementById(id).addEventListener('click', () => { if (!panel.hidden) setOpen(false); });

  panel.addEventListener('submit', e => {
    e.preventDefault();
    const title = (new FormData(e.target).get('t') || '').toString().trim();
    if (!title) return;
    const t = { id: newId(), title, plannedOn: dayKey(), doneOn: null };
    todos().push(t);
    commit('PUT', `/api/todos/${t.id}`, { title, plannedOn: t.plannedOn }, `дело «${title}»`);
    panel.querySelector('.idea-add input').focus();
  });

  panel.addEventListener('click', e => {
    const act = e.target.closest('[data-act]')?.dataset.act;
    if (act === 'close') return setOpen(false);
    const t = find(e.target.closest('[data-id]')?.dataset.id);
    if (!t) return;
    if (act === 'done') {
      // день отметки — явно: офлайн-отметка за вторник уйдёт на сервер позже, но останется вторником
      t.doneOn = e.target.checked ? dayKey() : null;
      commit('PUT', `/api/todos/${t.id}/done`, { done: !!t.doneOn, day: t.doneOn ?? dayKey() }, `дело «${t.title}» ${t.doneOn ? 'сделано' : 'не сделано'}`);
    } else if (act === 'del') {
      sync.state.todos = todos().filter(x => x !== t);
      commit('DELETE', `/api/todos/${t.id}`, undefined, `удалить дело «${t.title}»`);
    }
  });

  panel.addEventListener('change', e => {
    if (e.target.dataset.act !== 'title') return;
    const t = find(e.target.closest('[data-id]')?.dataset.id);
    const title = e.target.value.trim();
    if (!t || title === t.title) return;
    if (!title) { e.target.value = t.title; return; }     // пустое название не сохраняем — удаление отдельной кнопкой
    t.title = title;
    commit('PUT', `/api/todos/${t.id}`, { title }, `дело «${title}»`);
  });

  panel.addEventListener('keydown', e => {
    if (e.key === 'Enter' && e.target.dataset.act === 'title') e.target.blur();
  });
  document.addEventListener('keydown', e => {
    if (e.key === 'Escape' && !panel.hidden && !e.target.closest('#todos input')) setOpen(false);
  });

  // свежий снимок с сервера; новый день (вкладка открыта с вечера) — список пересчитывается
  sync.onReplaced(() => { if (!panel.contains(document.activeElement)) render(); });
  document.addEventListener('visibilitychange', () => { if (!document.hidden && !panel.contains(document.activeElement)) render(); });
  render();
}
