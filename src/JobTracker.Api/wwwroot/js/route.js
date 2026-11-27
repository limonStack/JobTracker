// Проход по «Карте подготовки» раздела компании: вопросы из таблицы по порядку, панель «← / →»,
// «Изучено, дальше». Ссылки «9.14–9.19» разворачиваются в диапазон, ссылки после слова «новое» — новые темы.
// Место в маршруте помнит вкладка (sessionStorage) — это удобство, не пользовательские данные.

const RANGE = /^\s*[–—-]\s*$/;

/**
 * Маршрут по ячейкам таблицы. cells — ячейки по порядку, каждая — токены { text } или { code: '9.14' }.
 * exists(code) — есть ли такой вопрос. Результат — [{ code, isNew }] без повторов, в порядке таблицы.
 */
export function routeFromCells(cells, exists) {
  const out = [], seen = new Set();
  const add = (code, isNew) => {
    if (seen.has(code) || !exists(code)) return;
    seen.add(code);
    out.push({ code, isNew });
  };
  for (const cell of cells) {
    let isNew = false, prev = null, range = false;
    for (const t of cell) {
      if (t.code == null) {
        if (t.text.includes(';')) isNew = false;            // «…; новое — 12.26» — «новое» действует до следующей «;»
        if (/новое/i.test(t.text)) isNew = true;
        range = prev != null && RANGE.test(t.text);
        if (!range && t.text.trim()) prev = null;
        continue;
      }
      if (range && prev) {
        const [s, a] = prev.split('.').map(Number), [s2, b] = t.code.split('.').map(Number);
        if (s === s2) for (let n = a + 1; n < b; n++) add(`${s}.${n}`, isNew);
      }
      add(t.code, isNew);
      prev = t.code;
      range = false;
    }
  }
  return out;
}

function cellTokens(td) {
  const tokens = [];
  const walk = node => {
    for (const n of node.childNodes) {
      if (n.nodeType === Node.TEXT_NODE) tokens.push({ text: n.textContent });
      else if (n.tagName === 'A' && /^#q\d+\.\d+$/.test(n.getAttribute('href') ?? '')) tokens.push({ code: n.getAttribute('href').slice(2) });
      else if (n.tagName === 'A') tokens.push({ text: n.textContent });   // ссылка на раздел целиком — в маршрут не входит
      else walk(n);
    }
  };
  walk(td);
  return tokens;
}

const esc = s => String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

/**
 * navigate(id) — переход с записью в истории; item(code) — { code, title, mark } или null;
 * isDone(code), toggleDone(code) — отметки «изучено».
 */
export function startRoutes({ navigate, item, isDone, toggleDone }) {
  const routes = new Map();                                 // slug раздела → { name, all, fresh }
  for (const section of document.querySelectorAll('main section[data-kind="company"]')) {
    const h1 = section.querySelector('h1');
    const h2 = [...section.querySelectorAll('h2')].find(h => h.textContent.trim() === 'Карта подготовки');
    const table = h2?.nextElementSibling?.tagName === 'TABLE' ? h2.nextElementSibling : null;
    if (!h1?.id || !table) continue;
    const cells = [...table.querySelectorAll('tbody td')].map(cellTokens);
    const all = routeFromCells(cells, code => !!item(code));
    if (!all.length) continue;
    const route = { slug: h1.id, name: h1.textContent.replace(/^\d+\.\s*/, ''), all, fresh: all.filter(x => x.isNew) };
    routes.set(h1.id, route);

    const tools = document.createElement('div');
    tools.className = 'route-tools';
    tools.innerHTML =
      `<button type="button" class="route-start" data-mode="all">▶ Пройти карту по порядку</button>` +
      (route.fresh.length ? `<button type="button" class="route-start" data-mode="fresh">★ Только новые темы</button>` : '') +
      `<span class="route-stat"></span>`;
    tools.addEventListener('click', e => {
      const b = e.target.closest('.route-start');
      if (b) start(route.slug, b.dataset.mode);
    });
    table.before(tools);
    route.tools = tools;
  }

  const bar = document.createElement('div');
  bar.className = 'route-bar';
  bar.hidden = true;
  document.body.append(bar);

  let state = null;                                         // { slug, mode, i }
  const list = () => { const r = state && routes.get(state.slug); return r ? (state.mode === 'fresh' ? r.fresh : r.all) : []; };
  const save = () => { try { state ? sessionStorage.setItem('route', JSON.stringify(state)) : sessionStorage.removeItem('route'); } catch { /* нет хранилища — маршрут живёт до перезагрузки */ } };

  function start(slug, mode) {
    const r = routes.get(slug);
    const l = mode === 'fresh' ? r.fresh : r.all;
    const first = l.findIndex(x => !isDone(x.code));        // продолжить с первой неизученной
    state = { slug, mode, i: first < 0 ? 0 : first };
    open();
  }
  function open() {
    const x = list()[state.i];
    save();
    render();
    if (x) navigate('q' + x.code);
  }
  function step(d) {
    const l = list();
    if (state.i + d < 0 || state.i + d >= l.length) return;
    state.i += d;
    open();
  }
  function doneAndNext() {
    const x = list()[state.i];
    if (x && !isDone(x.code)) toggleDone(x.code);
    if (state.i < list().length - 1) step(1); else render();
  }
  function close() { state = null; save(); render(); }

  function render() {
    for (const r of routes.values()) {
      const all = r.all.filter(x => isDone(x.code)).length, fresh = r.fresh.filter(x => isDone(x.code)).length;
      r.tools.querySelector('.route-stat').textContent =
        `изучено ${all} из ${r.all.length}` + (r.fresh.length ? `, новых — ${fresh} из ${r.fresh.length}` : '');
    }
    const l = list(), x = l[state?.i];
    bar.hidden = !x;
    if (!x) return;
    const r = routes.get(state.slug), it = item(x.code);
    const k = l.filter(y => isDone(y.code)).length;
    const finished = k === l.length;
    bar.innerHTML =
      `<span class="rb-name" title="Маршрут по карте подготовки">${esc(r.name)}${state.mode === 'fresh' ? ' · новое' : ''}</span>` +
      `<button type="button" data-act="prev" title="Предыдущая тема ( [ )"${state.i ? '' : ' disabled'}>←</button>` +
      `<span class="rb-pos">${state.i + 1} / ${l.length}</span>` +
      `<button type="button" data-act="next" title="Следующая тема ( ] )"${state.i < l.length - 1 ? '' : ' disabled'}>→</button>` +
      `<a class="rb-cur" href="#q${x.code}" title="К началу темы">${isDone(x.code) ? '✓ ' : ''}${x.code} ${esc(it?.title ?? '')}${x.isNew ? ' <span class="rb-new">новое</span>' : ''}</a>` +
      `<button type="button" data-act="done" class="rb-done">${finished ? '🎉 Всё изучено' : isDone(x.code) ? 'Дальше' : '✓ Изучено, дальше'}</button>` +
      `<span class="rb-k">${k}/${l.length}</span>` +
      `<button type="button" data-act="close" title="Закончить проход">✕</button>`;
  }

  bar.addEventListener('click', e => {
    const act = e.target.closest('button')?.dataset.act;
    if (act === 'prev') step(-1);
    else if (act === 'next') step(1);
    else if (act === 'done') doneAndNext();
    else if (act === 'close') close();
  });
  document.addEventListener('keydown', e => {
    if (bar.hidden || e.ctrlKey || e.metaKey || e.altKey) return;
    const el = document.activeElement;
    if (/^(INPUT|TEXTAREA|SELECT)$/.test(el?.tagName ?? '') || el?.isContentEditable) return;
    if (e.key === ']') { e.preventDefault(); step(1); }
    else if (e.key === '[') { e.preventDefault(); step(-1); }
  });

  try {
    const saved = JSON.parse(sessionStorage.getItem('route') ?? 'null');
    if (saved && routes.has(saved.slug)) state = saved;
  } catch { /* нет хранилища */ }
  render();

  return {
    refresh: render,
    /** прогресс маршрута для счётчика раздела компании в меню */
    progress(slug) {
      const r = routes.get(slug);
      return r ? { k: r.all.filter(x => isDone(x.code)).length, n: r.all.length } : null;
    },
  };
}
