// Хук Claude Code (UserPromptSubmit): в сообщении ссылка на вакансию, а /interview-prep не вызван —
// подсказать Claude этот skill. Решение остаётся за Claude: пользователь мог просто спросить про вакансию.
// На вход (stdin) — JSON с полем prompt. Ответ — JSON с additionalContext, который Claude видит вместе с сообщением.
// Хук никогда не мешает отправке сообщения: при любой ошибке молча выходит с кодом 0.
import { existsSync, readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');

// сайты вакансий; у DOU в ссылке есть слаг компании — по нему ищем готовый раздел
const SITES = [
  { re: /https?:\/\/jobs\.dou\.ua\/companies\/([^/\s]+)\/vacancies\/\d+[^\s]*/i, company: m => m[1] },
  { re: /https?:\/\/(?:www\.)?djinni\.co\/jobs\/\d+[^\s]*/i },
  { re: /https?:\/\/(?:www\.)?work\.ua\/(?:ru\/|en\/)?jobs\/\d+[^\s]*/i },
  { re: /https?:\/\/(?:www\.)?robota\.ua\/[^\s]*vacancy\d+[^\s]*/i },
];

/** Вакансия в тексте: { url, companySlug } или null. */
export function findVacancy(prompt) {
  for (const site of SITES) {
    const m = prompt.match(site.re);
    if (m) return { url: m[0], companySlug: site.company?.(m)?.toLowerCase() ?? null };
  }
  return null;
}

/** Готовый раздел компании в контенте (id h1 вида «25-acme»): { id, heading } или null. */
export function findSection(companySlug, files) {
  if (!companySlug) return null;
  const slug = companySlug.replace(/[^a-z0-9-]/g, '');
  if (!slug) return null;
  for (const text of files) {
    const m = text.match(new RegExp(`<h1 id="(\\d+-${slug})">([^<]+)</h1>`, 'i'));
    if (m) return { id: m[1], heading: m[2] };
  }
  return null;
}

/** Текст подсказки или null, если подсказывать нечего. */
export function hint(prompt, files) {
  if (/(^|\s)\/interview-prep\b/.test(prompt)) return null;   // skill уже вызван
  const vacancy = findVacancy(prompt);
  if (!vacancy) return null;

  const section = findSection(vacancy.companySlug, files);
  return [
    `В сообщении ссылка на вакансию: ${vacancy.url}.`,
    'Если пользователь хочет подготовиться к ней или добавить её в трекер — используй skill interview-prep (Skill tool, args — ссылка).',
    'Если он просто спрашивает о вакансии — skill не запускай, но можешь предложить его одной строкой.',
    section ? `Раздел этой компании уже есть: «${section.heading}» (#${section.id}) — его обновлять, а не создавать второй.` : null,
  ].filter(Boolean).join(' ');
}

const isMain = process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href;
if (isMain) {
  try {
    let raw = '';
    for await (const chunk of process.stdin) raw += chunk;
    const prompt = JSON.parse(raw).prompt ?? '';
    const files = ['content/answers.html', 'content/private/answers.private.html']
      .map(f => join(root, f)).filter(existsSync).map(f => readFileSync(f, 'utf8'));
    const text = hint(prompt, files);
    if (text) console.log(JSON.stringify({ hookSpecificOutput: { hookEventName: 'UserPromptSubmit', additionalContext: text } }));
  } catch { /* подсказка не обязательна — сообщение уходит как есть */ }
  process.exit(0);
}
