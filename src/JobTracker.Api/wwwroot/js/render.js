// Строит меню и тексты ответов из GET /api/content — ту же разметку, что была в 300questions/ANSWERS.html:
// на неё опираются поиск, карточка ответа и выделения (они ищут текст раздела, поэтому важны и переносы строк).

const ICON = { must: '🔥', frequent: '⭐', senior: '🎓', none: '' };
const esc = s => String(s ?? '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
const withMark = (text, importance) => ICON[importance] ? `${text} ${ICON[importance]}` : text;

function sectionHtml(s) {
  const parts = [`<h1 id="${esc(s.slug)}">${s.number}. ${esc(s.title)}</h1>`, s.introHtml];
  for (const g of s.groups) {
    if (g.title) parts.push(`<h2>${esc(g.title)}</h2>`, g.introHtml);
    for (const q of g.questions)
      parts.push(`<h3 id="q${q.code}"><a class="anchor" href="#q${q.code}">#</a>${withMark(`${q.code} ${esc(q.title)}`, q.importance)}</h3>`, q.bodyHtml);
  }
  return `<section${s.kind === 'company' ? ' data-kind="company"' : ''}>${parts.filter(Boolean).join('\n')}\n</section>`;
}

function menuHtml(s) {
  const items = s.groups.flatMap(g => g.questions)
    .map(q => `<li><a href="#q${q.code}">${withMark(`${q.code} ${esc(q.title)}`, q.importance)}</a></li>`).join('');
  const kind = s.kind === 'company' ? ' data-kind="company"' : '';
  return `<details${kind}><summary><a href="#${esc(s.slug)}">${s.number}. ${esc(s.title)}</a></summary><ul>${items}</ul></details>`;
}

/**
 * Тексты ответов: копия в браузере (IndexedDB) с версией. С сервера — только если версия изменилась
 * (ETag → 304 без тела); сервер недоступен — из копии. Без копии и без сервера показать нечего.
 */
async function loadContent(storage) {
  const cached = await storage.get('content');
  try {
    const r = await fetch('/api/content', { headers: cached ? { 'If-None-Match': `"${cached.version}"` } : {} });
    if (r.status === 304 && cached) return cached;
    if (r.ok) {
      const fresh = await r.json();
      await storage.set('content', fresh);
      return fresh;
    }
    if (!cached) throw new Error(r.status === 404 ? 'Тексты ответов ещё не импортированы — проверьте папку content' : `${r.status} ${r.statusText}`);
  } catch (err) {
    if (!cached) throw new Error(err instanceof TypeError
      ? 'Сервер недоступен, а копии текстов в браузере ещё нет — откройте страницу один раз при запущенном сервере'
      : err.message);
  }
  return cached;
}

export async function renderContent(storage) {
  const content = await loadContent(storage);

  // разделы под вакансии компаний (skill interview-prep) — отдельным блоком под общей базой
  const general = content.sections.filter(s => s.kind !== 'company');
  const companies = content.sections.filter(s => s.kind === 'company');
  // у каждого списка свой контейнер: поиск переставляет разделы по релевантности внутри него
  document.getElementById('toc').outerHTML = `<div class="toc-general">${general.map(menuHtml).join('')}</div>`
    + (companies.length ? `<div class="toc-companies"><div class="toc-title">🏢 Компании</div>${companies.map(menuHtml).join('')}</div>` : '');
  document.querySelector('main').innerHTML = content.sections.map(sectionHtml).join('');
  return content;
}
