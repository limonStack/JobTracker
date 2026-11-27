// Проверка контента: то, что сломает импорт при старте или навигацию по ссылкам.
//   node check-content.mjs   — код выхода 1, если есть ошибки
import { loadAll } from './content-lib.mjs';

const files = loadAll();
const errors = [];
const warnings = [];
const all = files.flatMap(f => f.parsed.sections);

for (const s of all) {
  if (s.error) { errors.push(`${s.file}: ${s.error}`); continue; }
  const where = `раздел «${s.heading}» (${s.kind})`;
  if (s.number == null) { errors.push(`${where}: заголовок не по формату «N. Название»`); continue; }
  if (!s.id) warnings.push(`${where}: у h1 нет id — на раздел нельзя сослаться`);
  if (s.dataKind != null && s.dataKind !== 'company') errors.push(`${where}: неизвестный data-kind «${s.dataKind}» — импорт упадёт (допустимо: company)`);
  if (s.company && s.kind !== 'личный') errors.push(`${where}: раздел компании в публичном файле — всё о компании только в личный`);
  if (s.company && s.questions.length) errors.push(`${where}: в разделе компании есть вопросы h3 — новые темы идут в тематические разделы, здесь только таблица ссылок (append-questions.mjs)`);
  if (s.kind === 'публичный') {
    const privateIds = new Set(all.filter(x => x.kind === 'личный').flatMap(x => x.ids ?? []));
    for (const link of new Set(s.links)) if (privateIds.has(link))
      errors.push(`${where}: ссылка #${link} ведёт в личный файл — на GitHub она будет битой`);
  }

  s.questions.forEach((q, i) => {
    const [sec, num] = q.code.split('.').map(Number);
    if (sec !== s.number) errors.push(`${where}: вопрос q${q.code} — номер не из этого раздела`);
    if (num !== i + 1) errors.push(`${where}: вопрос q${q.code} — ожидался номер ${s.number}.${i + 1} (нумерация подряд с 1)`);
    if (!q.mark) errors.push(`${where}: у вопроса ${q.code} нет метки 🔥/⭐/🎓 в конце заголовка`);
    if (!q.raw.includes(`<a class="anchor" href="#q${q.code}">#</a>`)) warnings.push(`${where}: у вопроса ${q.code} нет якоря «#»`);
  });
}

// номера разделов и id — общие для всех файлов (FileContentSource падает на дубликате раздела)
const byNumber = Map.groupBy(all.filter(s => s.number != null), s => s.number);
for (const [n, list] of byNumber) if (list.length > 1) errors.push(`Раздел ${n} встречается ${list.length} раза: ${list.map(s => `«${s.title}» (${s.kind})`).join(', ')}`);

const idCount = new Map();
for (const s of all) for (const id of s.ids ?? []) idCount.set(id, (idCount.get(id) ?? 0) + 1);
for (const [id, n] of idCount) if (n > 1) errors.push(`id «${id}» встречается ${n} раза`);

for (const s of all) for (const link of new Set(s.links ?? []))
  if (!idCount.has(link)) errors.push(`раздел ${s.number} (${s.kind}): битая ссылка #${link}`);

for (const f of files) {
  if (!f.parsed.text.includes('<main>') || !f.parsed.text.includes('</main>')) errors.push(`${f.path}: нет <main> — разделы должны лежать в main > section`);
  const counts = f.parsed.sections.map(s => `${s.number}:${s.questions.length}`).join(' ');
  const total = f.parsed.sections.reduce((n, s) => n + s.questions.length, 0);
  console.log(`${f.kind} файл: разделов ${f.parsed.sections.length}, вопросов ${total} (${counts})`);
}

for (const w of warnings) console.log(`⚠ ${w}`);
for (const e of errors) console.log(`✖ ${e}`);
console.log(errors.length ? `\nОшибок: ${errors.length}` : '\nОшибок нет.');
process.exitCode = errors.length ? 1 : 0;
