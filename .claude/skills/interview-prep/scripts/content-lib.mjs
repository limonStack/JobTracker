// Общий разбор файлов контента для скриптов skill-а (без зависимостей).
// Формат — как в AnswersHtmlParser: main > section; h1 «N. Название»; h2 — группа; h3 id="qN.M" — вопрос.
import { existsSync, readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

export const root = resolve(dirname(fileURLToPath(import.meta.url)), '../../../..');

export const contentFiles = [
  { path: join(root, 'content/answers.html'), kind: 'публичный' },
  { path: join(root, 'content/private/answers.private.html'), kind: 'личный' },
];

const stripTags = s => s.replace(/<[^>]+>/g, '')
  .replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"').replace(/&#39;/g, "'").replace(/&amp;/g, '&')
  .replace(/\s+/g, ' ').trim();

export const MARKS = ['🔥', '⭐', '🎓'];

/** Разделы файла: { number, id, title, kind, file, html, questions: [{ code, number, title, mark }] }. */
export function parseFile({ path, kind }) {
  if (!existsSync(path)) return null;
  const text = readFileSync(path, 'utf8');
  const sections = [];
  const sectionRe = /<section>([\s\S]*?)<\/section>/g;
  for (const m of text.matchAll(sectionRe)) {
    const html = m[1];
    const h1 = html.match(/<h1(?:\s+id="([^"]*)")?[^>]*>([\s\S]*?)<\/h1>/);
    if (!h1) { sections.push({ error: 'раздел без h1', file: path, kind }); continue; }
    const heading = stripTags(h1[2]);
    const hm = heading.match(/^(\d+)\.\s+(.+)$/);
    const questions = [];
    for (const q of html.matchAll(/<h3 id="q([^"]+)">([\s\S]*?)<\/h3>/g)) {
      const code = q[1];
      // «#» — ссылка-якорь, не часть названия
      let title = stripTags(q[2].replace(/<a class="anchor"[^>]*>[\s\S]*?<\/a>/, ''));
      if (title.startsWith(code + ' ')) title = title.slice(code.length + 1);
      const mark = MARKS.find(x => title.endsWith(x)) ?? null;
      if (mark) title = title.slice(0, -mark.length).trim();
      questions.push({ code, title, mark, raw: q[0] });
    }
    sections.push({
      number: hm ? Number(hm[1]) : null,
      title: hm ? hm[2] : heading,
      heading,
      id: h1[1] ?? null,
      kind,
      file: path,
      html,
      questions,
      ids: [...html.matchAll(/\sid="([^"]+)"/g)].map(x => x[1]),
      links: [...html.matchAll(/href="#([^"]+)"/g)].map(x => x[1]),
    });
  }
  return { text, sections };
}

export function loadAll() {
  return contentFiles.map(f => ({ ...f, parsed: parseFile(f) })).filter(f => f.parsed);
}
