// Оглавление базы знаний: разделы и вопросы из публичного и личного файла.
//   node list-questions.mjs              — все разделы и вопросы
//   node list-questions.mjs --sections   — только разделы
//   node list-questions.mjs 12 23        — вопросы выбранных разделов
import { loadAll } from './content-lib.mjs';

const args = process.argv.slice(2);
const sectionsOnly = args.includes('--sections');
const only = new Set(args.filter(a => /^\d+$/.test(a)).map(Number));

const sections = loadAll().flatMap(f => f.parsed.sections).filter(s => s.number != null)
  .sort((a, b) => a.number - b.number);

for (const s of sections) {
  if (only.size && !only.has(s.number)) continue;
  console.log(`## ${s.number}. ${s.title}  [${s.kind}${s.company ? ', компания' : ''}, #${s.id}, вопросов: ${s.questions.length}]`);
  if (sectionsOnly) continue;
  for (const q of s.questions) console.log(`${q.code} ${q.title} ${q.mark ?? '(без метки)'}`);
}

const used = sections.map(s => s.number);
let next = Math.max(0, ...used) + 1;
console.log(`\nЗанятые номера разделов: ${used.join(', ')}. Следующий свободный: ${next}.`);
