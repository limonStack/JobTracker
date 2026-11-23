// Вставить раздел в файл контента или заменить существующий (по id h1).
//   node insert-section.mjs <private|public> <файл-раздела.html> [--replace]
// Файл раздела — один <section>…</section> с <h1 id="…">N. Название</h1>.
// Перед записью копия файла кладётся в backups/content/ (папка вне git).
import { copyFileSync, existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { basename, join } from 'node:path';
import { contentFiles, loadAll, root } from './content-lib.mjs';

const [target, sectionPath, ...rest] = process.argv.slice(2);
const replace = rest.includes('--replace');
const file = { private: contentFiles[1], public: contentFiles[0] }[target];
if (!file || !sectionPath) {
  console.error('Использование: node insert-section.mjs <private|public> <файл-раздела.html> [--replace]');
  process.exit(2);
}

const section = readFileSync(sectionPath, 'utf8').trim();
const h1 = section.match(/^<section><h1 id="([^"]+)">(\d+)\.\s+[^<]+<\/h1>/);
if (!h1 || !section.endsWith('</section>') || (section.match(/<section>/g) ?? []).length !== 1) {
  console.error('Раздел должен начинаться с <section><h1 id="…">N. Название</h1> и быть одним <section>…</section>.');
  process.exit(1);
}
const [, id, numberText] = h1;
const number = Number(numberText);

// номер раздела общий для обоих файлов: дубликат уронит импорт при старте приложения
const clash = loadAll().flatMap(f => f.parsed.sections).find(s => s.number === number && s.id !== id);
if (clash) {
  console.error(`Номер ${number} уже занят разделом «${clash.title}» (${clash.kind} файл). Возьмите свободный: node list-questions.mjs --sections`);
  process.exit(1);
}

if (!existsSync(file.path)) {
  mkdirSync(join(file.path, '..'), { recursive: true });
  writeFileSync(file.path, `<!doctype html>\n<!-- Личные разделы. Не коммитить: папка в .gitignore. -->\n<html lang="ru"><head><meta charset="utf-8"><title>private</title></head><body>\n<main></main>\n</body></html>\n`);
}
let html = readFileSync(file.path, 'utf8');

const start = html.indexOf(`<section><h1 id="${id}">`);
if (start >= 0) {
  if (!replace) {
    console.error(`Раздел #${id} уже есть в файле. Чтобы заменить его, добавьте --replace.`);
    process.exit(1);
  }
  const end = html.indexOf('</section>', start) + '</section>'.length;
  html = html.slice(0, start) + section + html.slice(end);
  console.log(`Раздел #${id} заменён.`);
} else {
  const main = html.lastIndexOf('</main>');
  if (main < 0) { console.error(`В ${file.path} нет </main>.`); process.exit(1); }
  html = html.slice(0, main) + section + html.slice(main);
  console.log(`Раздел #${id} добавлен.`);
}

const backupDir = join(root, 'backups', 'content');
mkdirSync(backupDir, { recursive: true });
const stamp = new Date().toISOString().replace(/[:.]/g, '-');
const backup = join(backupDir, `${basename(file.path, '.html')}.${stamp}.html`);
copyFileSync(file.path, backup);

writeFileSync(file.path, html);
console.log(`Файл: ${file.path}\nКопия до изменения: ${backup}`);
