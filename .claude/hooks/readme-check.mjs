// Хук Claude Code (PostToolUse, Bash|PowerShell): после `git commit` с крупным изменением
// (новый модуль API, сайт вакансий, фоновая задача, проект, пакеты, модуль фронтенда, CI или просто много файлов),
// в котором README не менялся, — попросить Claude проверить README. Переписывает README сам Claude: хук только
// замечает. Один коммит — одно напоминание. Любая ошибка — выход с кодом 0.
//   node readme-check.mjs              — как хук (JSON на stdin)
//   node readme-check.mjs --ref <sha>  — проверить конкретный коммит вручную
import { execFileSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const BIG_COMMIT = 12;   // столько изменённых файлов в src/ — уже «глобальное» изменение

/**
 * Почему коммит стоит отразить в README. files — [{ status: 'A'|'M'|'D'|'R…', path }],
 * packagesChanged — менялись ли PackageReference, existedBefore(dir) — была ли папка до коммита.
 * Пустой список — README трогать незачем.
 */
export function readmeReasons(files, { packagesChanged = false, existedBefore = () => true } = {}) {
  if (files.some(f => f.path === 'README.md')) return [];     // README уже обновлён в этом коммите
  const added = files.filter(f => f.status.startsWith('A')).map(f => f.path);
  const reasons = new Set();
  const name = p => p.split('/').pop();

  for (const p of added) {
    if (/^src\/JobTracker\.Api\/Endpoints\/.+Endpoints\.cs$/.test(p)) reasons.add(`новый модуль API: ${name(p)}`);
    else if (/^src\/JobTracker\.Infrastructure\/VacancyChecking\/.+Checker\.cs$/.test(p) && !/Html|Resolver/.test(p)) reasons.add(`новый сайт проверки вакансий: ${name(p)}`);
    else if (/^src\/JobTracker\.Api\/BackgroundJobs\/.+\.cs$/.test(p)) reasons.add(`новая фоновая задача: ${name(p)}`);
    else if (/^src\/[^/]+\/[^/]+\.csproj$/.test(p)) reasons.add(`новый проект: ${name(p)}`);
    else if (/^src\/JobTracker\.Api\/wwwroot\/(js\/)?[^/]+\.js$/.test(p)) reasons.add(`новый модуль фронтенда: ${name(p)}`);
    const domain = p.match(/^src\/JobTracker\.Domain\/([^/]+)\//);
    if (domain && !existedBefore(`src/JobTracker.Domain/${domain[1]}`)) reasons.add(`новый модуль домена: ${domain[1]}`);
  }
  if (packagesChanged) reasons.add('изменились NuGet-пакеты (стек)');
  if (files.some(f => f.path === '.github/workflows/ci.yml')) reasons.add('изменился CI');
  if (files.some(f => /^src\/[^/]+\/[^/]+\.csproj$/.test(f.path) && f.status.startsWith('D'))) reasons.add('удалён проект');

  const srcCount = files.filter(f => f.path.startsWith('src/') && !f.path.includes('/Migrations/')).length;
  if (srcCount >= BIG_COMMIT) reasons.add(`крупное изменение: ${srcCount} файлов в src/`);
  return [...reasons];
}

/** Что сказать Claude, или null. */
export function readmeMessage(sha, subject, reasons) {
  if (!reasons.length) return null;
  return [
    `Коммит ${sha} «${subject}» — заметное изменение (${reasons.join('; ')}), а README.md в нём не менялся.`,
    'Проверь, нужно ли обновить README: возможности, запуск, стек или архитектура. README в репозитории публичный и намеренно короткий —',
    'добавляй одну-две строки в существующие разделы, без внутренних деталей (они в CLAUDE.md), без личных данных и названий реальных компаний.',
    'Если README уже точен — ничего не меняй и не упоминай это. Если обновил — отдельный коммит.',
  ].join(' ');
}

const git = (...args) => execFileSync('git', args, { cwd: root, encoding: 'utf8', windowsHide: true }).trim();

function inspect(ref) {
  const sha = git('rev-parse', '--short', ref);
  const subject = git('log', '-1', '--format=%s', ref);
  const files = git('show', '--name-status', '--format=', '-M', ref).split('\n').filter(Boolean)
    .map(line => { const [status, ...paths] = line.split('\t'); return { status, path: paths.at(-1) }; });
  const packagesChanged = /^[+-].*PackageReference/m.test(git('show', '--format=', ref, '--', '*.csproj', 'Directory.Build.props'));
  const hasParent = (() => { try { git('rev-parse', '--verify', '--quiet', `${ref}~1`); return true; } catch { return false; } })();
  const existedBefore = dir => hasParent && git('ls-tree', '--name-only', `${ref}~1`, dir) !== '';
  return { sha, subject, reasons: readmeReasons(files, { packagesChanged, existedBefore }) };
}

const isMain = process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href;
if (isMain) {
  try {
    const refArg = process.argv.indexOf('--ref');
    if (refArg > 0) {
      const { sha, subject, reasons } = inspect(process.argv[refArg + 1]);
      console.log(readmeMessage(sha, subject, reasons) ?? `Коммит ${sha}: README обновлять незачем.`);
    } else {
      let raw = '';
      for await (const chunk of process.stdin) raw += chunk;
      const input = JSON.parse(raw);
      if (/\bgit\b[^\n]*\bcommit\b/.test(input.tool_input?.command ?? '')) {
        const { sha, subject, reasons } = inspect('HEAD');
        // одно напоминание на коммит: повторная команда или неудачный коммит (HEAD не сдвинулся) — молчим
        const stateDir = join(tmpdir(), 'jobtracker-hooks');
        const seen = join(stateDir, 'readme-check-last.txt');
        const last = existsSync(seen) ? readFileSync(seen, 'utf8').trim() : '';
        if (sha !== last) {
          mkdirSync(stateDir, { recursive: true });
          writeFileSync(seen, sha);
          const text = readmeMessage(sha, subject, reasons);
          if (text) console.log(JSON.stringify({ hookSpecificOutput: { hookEventName: 'PostToolUse', additionalContext: text } }));
        }
      }
    }
  } catch (err) {
    console.error(`readme-check hook: ${err.message}`);
  }
  process.exit(0);
}
