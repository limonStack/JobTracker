'use strict';
// Отклики и собеседования: данные — в API (SQLite), страница только показывает и отправляет изменения.
// Разметка и поведение перенесены из трекера в 300questions/ANSWERS.html.
(() => {
  const panel = document.getElementById('jobs');

  // этапы по порядку воронки; отказ и «нет ответа» — финал, дошедший этап сервер помнит в reachedStage
  const STAGES = [
    { id: 'applied', label: 'Отклик', color: '#71717a' },
    { id: 'replied', label: 'Ответили', color: '#4fa3e0' },
    { id: 'hr', label: 'HR', color: '#a78bfa' },
    { id: 'tech', label: 'Тех. собес', color: '#f0883e' },
    { id: 'offer', label: 'Оффер', color: '#22c55e' },
    { id: 'rejected', label: 'Отказ', color: '#ef4444' },
    { id: 'silent', label: 'Нет ответа', color: '#52525b' },
  ];
  const FUNNEL = ['applied', 'replied', 'hr', 'tech', 'offer'];
  const MONTHS = ['Январь', 'Февраль', 'Март', 'Апрель', 'Май', 'Июнь', 'Июль', 'Август', 'Сентябрь', 'Октябрь', 'Ноябрь', 'Декабрь'];
  const OLD_STORE = 'net-interview-jobs-v1';

  const stageOf = id => STAGES.find(s => s.id === id) || STAGES[0];
  const reached = j => Math.max(0, FUNNEL.indexOf(j.reachedStage));
  const esc = s => String(s ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
  const escAttr = s => esc(s).replace(/"/g, '&quot;');
  const plural = (n, one, few, many) => { const a = n % 10, b = n % 100; return a === 1 && b !== 11 ? one : a >= 2 && a <= 4 && (b < 12 || b > 14) ? few : many; };
  const dayKey = (d = new Date()) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
  const fmtDate = key => key ? key.split('-').reverse().join('.') : '';
  const fmtShort = key => key ? key.slice(8) + '.' + key.slice(5, 7) : '';
  // название компании из ссылки DOU: …/companies/team-international/… → «Team International»
  const companyFromUrl = u => {
    const slug = (u.match(/dou\.ua\/companies\/([^/?#]+)/i) || [])[1];
    return slug ? decodeURIComponent(slug).split(/[-_]+/).map(w => w && w[0].toUpperCase() + w.slice(1)).join(' ') : '';
  };

  // ---------- API ----------
  async function api(method, url, body) {
    const res = await fetch(url, {
      method,
      headers: body === undefined ? {} : { 'Content-Type': 'application/json' },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
    if (!res.ok) {
      const problem = await res.json().catch(() => null);
      throw new Error(problem?.detail || problem?.title || `Ошибка ${res.status}`);
    }
    return res.status === 204 ? null : res.json();
  }

  let jobs = [];
  const openJobs = new Set();                         // раскрытые записи
  let adding = false, importing = false;
  let filter = 'all';                                 // выбранная плашка воронки
  let message = null;                                 // { error: bool, text }
  const FILTERS = {
    all: () => true,
    replied: j => reached(j) >= 1,
    hr: j => reached(j) >= 2,
    tech: j => reached(j) >= 3,
    offer: j => reached(j) >= 4,
    rejected: j => j.stage === 'rejected',
    active: j => ['replied', 'hr', 'tech'].includes(j.stage),
    inactive: j => j.vacancyStatus === 'inactive',
  };
  let checking = false;                               // идёт фоновая проверка всех вакансий
  const CHECK_ONE_ENABLED = false;                    // временно: проверка только кнопкой «Проверить все»

  // статус вакансии на сайте: значок у компании и подробная строка в раскрытой записи
  const VACANCY = {
    active: { icon: '●', label: 'вакансия открыта' },
    inactive: { icon: '🔒', label: 'вакансия закрыта' },
    checkFailed: { icon: '⚠', label: 'проверить не удалось' },
  };
  const fmtTime = iso => {
    const d = new Date(iso);
    return `${fmtShort(dayKey(d))} ${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`;
  };
  const vacancyTip = j => {
    const v = VACANCY[j.vacancyStatus];
    if (!v) return '';
    const closed = j.vacancyStatus === 'inactive' && j.closedAt ? `, закрытие замечено ${fmtTime(j.closedAt)}` : '';
    return `${v.label}${closed} · проверено ${fmtTime(j.lastCheckedAt)}`;
  };
  // своя колонка в строке: пустой span у непроверенных держит сетку, значки встают друг под другом
  const vacancyBadge = j => VACANCY[j.vacancyStatus]
    ? `<span class="job-vac ${j.vacancyStatus}" title="${escAttr(vacancyTip(j))}">${VACANCY[j.vacancyStatus].icon}</span>`
    : `<span class="job-vac"></span>`;

  // «Проверить все» идёт в фоне: опрашиваем ход, пока не закончится, потом перечитываем список
  async function checkAll() {
    checking = true;
    render();
    await api('POST', '/api/checks');
    let status;
    do {
      await new Promise(r => setTimeout(r, 1500));
      status = await api('GET', '/api/checks');
    } while (status.running);
    await load();
    checking = false;
    const r = status.lastResult;
    message = r
      ? { error: r.failed > 0, text: `Проверено вакансий: ${r.checked}, закрылось: ${r.closed}` + (r.failed ? `, не удалось проверить: ${r.failed}` : '') }
      : { error: true, text: 'Проверка завершилась ошибкой — подробности в логе приложения' };
  }

  // выполняет запрос и перерисовывает; ошибку сервера показывает над списком, а не в alert
  async function run(action) {
    try {
      message = null;
      await action();
    } catch (err) {
      message = { error: true, text: err.message };
    }
    render();
  }

  const replaceJob = updated => { jobs = jobs.map(j => j.id === updated.id ? updated : j); };
  const load = async () => { jobs = await api('GET', '/api/applications'); };
  const details = (j, patch) => ({ company: j.company, appliedOn: j.appliedOn, url: j.url, note: j.note, ...patch });

  // ---------- Отрисовка ----------
  function render() {
    const n = jobs.length;
    const count = f => jobs.filter(FILTERS[f]).length;
    const pct = k => n ? ` <small>${Math.round(k * 100 / n)}%</small>` : '';
    const replied = count('replied'), offers = count('offer'), rejected = count('rejected');
    // плашки воронки — фильтры списка: клик оставляет только эти записи, повторный клик снимает фильтр
    const funnel = [
      ['all', n, plural(n, 'отклик', 'отклика', 'откликов'), 'Все отклики'],
      ['replied', replied, 'ответили' + pct(replied), 'Ответили хотя бы раз'],
      ['hr', count('hr'), 'HR', 'Дошли до разговора с HR'],
      ['tech', count('tech'), 'тех. собес', 'Дошли до технического собеседования'],
      ['offer', offers, plural(offers, 'оффер', 'оффера', 'офферов'), 'Получили оффер'],
      ['rejected', rejected, plural(rejected, 'отказ', 'отказа', 'отказов'), 'Отказали'],
      ['active', count('active'), 'в процессе', 'Идут сейчас: этап «Ответили», «HR» или «Тех. собес» — без оффера, отказа и «нет ответа»'],
      ['inactive', count('inactive'), plural(count('inactive'), 'закрыта', 'закрыты', 'закрыты'), 'Вакансия на сайте закрыта — по последней проверке'],
    ].map(([f, v, l, tip]) => `<button type="button" class="jf${f === filter ? ' on' : ''}" data-act="filter" data-f="${f}" title="${tip}"><b>${v}</b>${l}</button>`).join('');

    const today = dayKey();
    // записи группами по месяцам отклика, новые сверху; у месяца — заголовок с числом откликов
    let month = '';
    const sorted = jobs.filter(FILTERS[filter]).sort((a, b) => b.appliedOn.localeCompare(a.appliedOn));
    const rows = sorted.map(j => {
      const ym = j.appliedOn.slice(0, 7), k = sorted.filter(x => x.appliedOn.startsWith(ym)).length;
      const head = ym === month ? '' : `<div class="job-month">${MONTHS[+ym.slice(5) - 1]} ${ym.slice(0, 4)}<span>${k} ${plural(k, 'отклик', 'отклика', 'откликов')}</span></div>`;
      month = ym;
      const st = stageOf(j.stage), last = j.events[j.events.length - 1];
      const opened = openJobs.has(j.id);
      const options = STAGES.map(s => `<option value="${s.id}"${s.id === j.stage ? ' selected' : ''}>${s.label}</option>`).join('');
      const closed = ['rejected', 'silent'].includes(j.stage) || j.vacancyStatus === 'inactive';
      return head + `<div class="job${closed ? ' closed' : ''}${opened ? ' open' : ''}" data-job="${j.id}">` +
        `<div class="job-row" data-act="toggle">` +
          `<span class="job-date">${fmtDate(j.appliedOn)}</span>` +
          vacancyBadge(j) +
          `<span class="job-name">` +
          (j.url ? `<a class="job-company" data-act="open" href="${escAttr(j.url)}" target="_blank" rel="noopener" title="Открыть вакансию">${esc(j.company)}<span>↗</span></a>`
            : `<span class="job-company">${esc(j.company)}</span>`) +
          `</span>` +
          `<select class="job-stage" data-act="stage" style="--st:${st.color}" aria-label="Этап">${options}</select>` +
          `<span class="job-last">${last ? `${fmtShort(last.date)} · ${esc(last.text)}` : j.note ? esc(j.note) : ''}</span>` +
          `<span class="job-arrow">${opened ? '▾' : '▸'}</span>` +
        `</div>` +
        (opened ? `<div class="job-detail">` +
          `<label class="job-field">Ссылка на вакансию<input data-act="url" inputmode="url" value="${escAttr(j.url)}" placeholder="https://…"></label>` +
          (j.url ? `<div class="job-check"><span>${VACANCY[j.vacancyStatus] ? `${VACANCY[j.vacancyStatus].icon} ${esc(vacancyTip(j))}` : 'вакансию ещё не проверяли'}</span>` +
            (CHECK_ONE_ENABLED ? `<button type="button" class="jobs-btn" data-act="check">Проверить сейчас</button>` : '') + `</div>` : '') +
          `<label class="job-field">Заметка<textarea data-act="note" rows="2" placeholder="Что важно: стек, зарплата, что спросили, что подтянуть">${esc(j.note)}</textarea></label>` +
          `<div class="job-events">${j.events.map(ev => `<div class="job-ev"><time>${fmtShort(ev.date)}</time><span>${esc(ev.text)}</span>` +
            `<button type="button" data-act="ev-del" data-ev="${ev.id}" title="Удалить событие">✕</button></div>`).join('') || '<div class="job-empty">событий пока нет</div>'}</div>` +
          `<form class="job-evadd" data-act="ev-add"><input type="date" name="d" value="${today}" required>` +
            `<input name="t" placeholder="Событие: разговор с HR, тестовое, тех. собес…" required><button type="submit" class="jobs-btn">Добавить</button></form>` +
          `<button type="button" class="jobs-btn danger" data-act="del">Удалить запись</button>` +
        `</div>` : '') +
      `</div>`;
    }).join('');

    panel.innerHTML =
      `<div class="jobs-head"><span class="jobs-title">💼 Собеседования</span>` +
        `<button type="button" class="jobs-btn primary" data-act="add-toggle">${adding ? 'Отмена' : '+ Отклик'}</button>` +
        `<button type="button" class="jobs-btn" data-act="check-all"${checking ? ' disabled' : ''} title="Проверить на сайтах, открыты ли ещё вакансии (в процессе, со ссылкой)">${checking ? '⟳ Проверяю…' : '⟳ Проверить все'}</button>` +
        `<button type="button" class="jobs-btn" data-act="import-toggle" title="Перенести записи из старого трекера">⬆ Импорт</button>` +
        `<button type="button" class="jobs-btn" data-act="csv" title="Скачать таблицу для Excel">⬇ CSV</button>` +
        `<a class="jobs-btn" href="/api/export" title="Резервная копия всех данных">⬇ JSON</a></div>` +
      (message ? `<div class="${message.error ? 'jobs-error' : 'jobs-info'}">${esc(message.text)}</div>` : '') +
      `<div class="jobs-funnel">${funnel}</div>` +
      (importing ? `<form class="jobs-import" data-act="import">` +
        `<span>Откройте старый трекер, нажмите F12 → Console и выполните <code>copy(localStorage.getItem('${OLD_STORE}'))</code> — данные окажутся в буфере обмена. Вставьте их сюда. Повторный импорт не создаёт дублей.</span>` +
        `<textarea name="json" rows="5" placeholder='[{"id":"…","d":"2026-09-21","c":"…"}]' required></textarea>` +
        `<button type="submit" class="jobs-btn primary">Импортировать</button></form>` : '') +
      (adding ? `<form class="jobs-add" data-act="add"><input type="date" name="d" value="${today}" required>` +
        `<input name="u" data-act="add-url" inputmode="url" placeholder="Ссылка на вакансию — вставьте, компания подставится"><input name="c" placeholder="Компания" required>` +
        `<input class="full" name="n" placeholder="Заметка (необязательно)"><button type="submit" class="jobs-btn primary full">Добавить отклик</button></form>` : '') +
      `<div class="jobs-list">${rows || `<div class="job-empty">${filter === 'all' ? 'Откликов пока нет' : 'Под этот фильтр записей нет'}</div>`}</div>`;
    if (adding) panel.querySelector('.jobs-add [name="u"]').focus();
  }

  function exportCsv() {
    const cell = v => `"${String(v ?? '').replace(/"/g, '""')}"`;
    const lines = [['Дата отклика', 'Компания', 'Вакансия', 'Этап', 'События', 'Заметка'].map(cell).join(';')];
    for (const j of jobs.slice().sort((a, b) => a.appliedOn.localeCompare(b.appliedOn)))
      lines.push([fmtDate(j.appliedOn), j.company, j.url, stageOf(j.stage).label, j.events.map(ev => `${fmtDate(ev.date)} ${ev.text}`).join('; '), j.note].map(cell).join(';'));
    const a = document.createElement('a');
    a.href = URL.createObjectURL(new Blob(['﻿' + lines.join('\r\n')], { type: 'text/csv;charset=utf-8' }));
    a.download = `собеседования-${dayKey()}.csv`;
    a.click();
    setTimeout(() => URL.revokeObjectURL(a.href), 1000);
  }

  // ---------- События интерфейса ----------
  panel.addEventListener('click', e => {
    const t = e.target.closest('[data-act]');
    if (!t) return;
    const j = jobs.find(x => x.id === t.closest('[data-job]')?.dataset.job);
    switch (t.dataset.act) {
      case 'add-toggle': adding = !adding; importing = false; render(); break;
      case 'import-toggle': importing = !importing; adding = false; render(); break;
      case 'csv': exportCsv(); break;
      case 'check-all': if (!checking) run(() => checkAll().finally(() => { checking = false; })); break;
      case 'check':
        t.disabled = true;
        t.textContent = 'Проверяю…';
        run(async () => replaceJob(await api('POST', `/api/applications/${j.id}/check`)));
        break;
      case 'filter': filter = filter === t.dataset.f ? 'all' : t.dataset.f; render(); break;
      case 'toggle': if (j) { if (openJobs.has(j.id)) openJobs.delete(j.id); else openJobs.add(j.id); render(); } break;
      case 'ev-del': run(async () => replaceJob(await api('DELETE', `/api/applications/${j.id}/events/${t.dataset.ev}`))); break;
      case 'del':
        if (confirm(`Удалить запись «${j.company}»?`))
          run(async () => { await api('DELETE', `/api/applications/${j.id}`); jobs = jobs.filter(x => x !== j); });
        break;
    }
  });
  // смена этапа: событие с сегодняшней датой пишет сервер (клик по выпадающему списку запись не раскрывает)
  panel.addEventListener('change', e => {
    const t = e.target, j = jobs.find(x => x.id === t.closest('[data-job]')?.dataset.job);
    if (!j) return;
    if (t.dataset.act === 'stage')
      run(async () => replaceJob(await api('PUT', `/api/applications/${j.id}/stage`, { stage: t.value })));
    else if (t.dataset.act === 'note')
      run(async () => replaceJob(await api('PUT', `/api/applications/${j.id}`, details(j, { note: t.value }))));
    else if (t.dataset.act === 'url')
      run(async () => replaceJob(await api('PUT', `/api/applications/${j.id}`, details(j, { url: t.value }))));
  });
  // вставили ссылку в форму нового отклика — пустое поле «Компания» заполняется само
  panel.addEventListener('input', e => {
    if (e.target.closest('.jobs-add') && e.target.name === 'c') delete e.target.dataset.auto;   // набрали сами — не трогаем
    if (e.target.dataset.act !== 'add-url') return;
    const c = e.target.form.elements.c, guess = companyFromUrl(e.target.value);
    if (guess && (!c.value || c.dataset.auto)) { c.value = guess; c.dataset.auto = '1'; }
  });
  panel.addEventListener('submit', e => {
    e.preventDefault();
    const f = new FormData(e.target), get = k => (f.get(k) || '').toString().trim();
    switch (e.target.dataset.act) {
      case 'add':
        run(async () => {
          jobs.push(await api('POST', '/api/applications', { company: get('c'), appliedOn: get('d'), url: get('u'), note: get('n') }));
          adding = false;
        });
        break;
      case 'ev-add': {
        const id = e.target.closest('[data-job]').dataset.job;
        run(async () => replaceJob(await api('POST', `/api/applications/${id}/events`, { date: get('d'), text: get('t') })));
        break;
      }
      case 'import':
        run(async () => {
          let records;
          try { records = JSON.parse(get('json')); } catch { throw new Error('Это не JSON — скопируйте данные целиком, как в подсказке.'); }
          const r = await api('POST', '/api/import', records);
          await load();
          importing = false;
          message = { error: r.errors.length > 0, text: `Импортировано: ${r.imported}, уже были: ${r.skipped}` + (r.errors.length ? `. Ошибки: ${r.errors.join('; ')}` : '') };
        });
        break;
    }
  });

  run(load);
})();
