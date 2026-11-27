// Дописать новые вопросы в конец тематического раздела (публичного или личного) со сквозной нумерацией.
//   node append-questions.mjs <номер-раздела> <файл-вопросов.html>
// Файл — фрагмент: необязательный <h2>Группа</h2> и вопросы с временными кодами NEW1, NEW2…:
//   <h3 id="qNEW1"><a class="anchor" href="#qNEW1">#</a>NEW1 Название 🔥</h3> …ответ…
// Ссылки между новыми вопросами — #qNEW1. Скрипт заменяет NEWk на N.M (следующие свободные номера раздела),
// дописывает в конец раздела (существующие номера и выделения не сдвигаются) и печатает соответствие кодов.
// В раздел компании вопросы не дописываются. Перед записью копия файла кладётся в backups/content/.
import { copyFileSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { basename, join } from 'node:path';
import { loadAll, root } from './content-lib.mjs';

const [numberText, fragmentPath] = process.argv.slice(2);
const number = Number(numberText);
if (!number || !fragmentPath) {
  console.error('Использование: node append-questions.mjs <номер-раздела> <файл-вопросов.html>');
  process.exit(2);
}

const target = loadAll().flatMap(f => f.parsed.sections.map(s => ({ ...s, path: f.path }))).find(s => s.number === number);
if (!target) { console.error(`Раздела ${number} нет. Список: node list-questions.mjs --sections`); process.exit(1); }
if (target.company) {
  console.error(`Раздел ${number} — раздел компании: новые темы туда не добавляются, только в тематические разделы.`);
  process.exit(1);
}

let fragment = readFileSync(fragmentPath, 'utf8').trim();
if (/<\/?(section|h1)[\s>]/.test(fragment)) { console.error('Во фрагменте не должно быть <section> и <h1>.'); process.exit(1); }
const temp = [...fragment.matchAll(/<h3 id="q(NEW\d+)">/g)].map(m => m[1]);
if (!temp.length) { console.error('Во фрагменте нет вопросов <h3 id="qNEW1">.'); process.exit(1); }
if (new Set(temp).size !== temp.length) { console.error('Временные коды повторяются.'); process.exit(1); }

const map = Object.fromEntries(temp.map((t, i) => [t, `${number}.${target.questions.length + i + 1}`]));
// длинные коды первыми: NEW12 не должен совпасть с NEW1
for (const t of [...temp].sort((a, b) => b.length - a.length)) {
  fragment = fragment.replaceAll(`#q${t}"`, `#q${map[t]}"`).replaceAll(`id="q${t}"`, `id="q${map[t]}"`)
    .replaceAll(`</a>${t} `, `</a>${map[t]} `).replaceAll(`>${t}</a>`, `>${map[t]}</a>`);
}
if (/NEW\d/.test(fragment)) { console.error('Остались временные коды без вопроса: ' + fragment.match(/NEW\d+/g).join(', ')); process.exit(1); }
for (const h of fragment.matchAll(/<h3 id="q[^"]+">[\s\S]*?<\/h3>/g))
  if (!/(🔥|⭐|🎓)<\/h3>$/.test(h[0])) { console.error('У вопроса нет метки 🔥/⭐/🎓: ' + h[0]); process.exit(1); }

let html = readFileSync(target.path, 'utf8');
const start = html.indexOf(`id="${target.id}"`);
const end = html.indexOf('</section>', start);
const body = fragment.replace(/^<hr \/>\s*/, '').replace(/\s*<hr \/>$/, '');
html = html.slice(0, end).trimEnd() + (target.questions.length ? '\n<hr />\n' : '\n') + body + '\n' + html.slice(end);

const backupDir = join(root, 'backups', 'content');
mkdirSync(backupDir, { recursive: true });
const backup = join(backupDir, `${basename(target.path, '.html')}.${new Date().toISOString().replace(/[:.]/g, '-')}.html`);
copyFileSync(target.path, backup);
writeFileSync(target.path, html);

console.log(`Раздел ${number} (${target.kind}): добавлено вопросов ${temp.length}.`);
for (const t of temp) console.log(`  ${t} → ${map[t]}`);
console.log(`Копия до изменения: ${backup}`);
