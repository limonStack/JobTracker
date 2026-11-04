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
  return `<section>${parts.filter(Boolean).join('\n')}\n</section>`;
}

function menuHtml(s) {
  const items = s.groups.flatMap(g => g.questions)
    .map(q => `<li><a href="#q${q.code}">${withMark(`${q.code} ${esc(q.title)}`, q.importance)}</a></li>`).join('');
  return `<details><summary><a href="#${esc(s.slug)}">${s.number}. ${esc(s.title)}</a></summary><ul>${items}</ul></details>`;
}

// тексты меняются редко: браузер держит копию и переспрашивает с ETag (ответ 304 без тела)
export async function renderContent() {
  const r = await fetch('/api/content');
  if (!r.ok) throw new Error(r.status === 404 ? 'Тексты ответов ещё не импортированы — проверьте папку content' : `${r.status} ${r.statusText}`);
  const content = await r.json();

  document.getElementById('toc').outerHTML = content.sections.map(menuHtml).join('');
  document.querySelector('main').innerHTML = content.sections.map(sectionHtml).join('');
  return content;
}
