// Проверка, что приложение импортирует контент: отдельный экземпляр на своём порту и временной базе.
// Рабочее приложение пользователя (порт 5196, jobtracker.db) не трогается.
//   node verify-app.mjs            — перед этим нужен dotnet build (контент копируется сборкой)
//   node verify-app.mjs --port 5299
import { spawn, execFileSync } from 'node:child_process';
import { mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { root } from './content-lib.mjs';

const portArg = process.argv.indexOf('--port');
const port = portArg > 0 ? Number(process.argv[portArg + 1]) : 5299;
const base = `http://localhost:${port}`;

try {
  await fetch(base + '/api/content');
  console.error(`Порт ${port} уже занят — укажите другой: --port N`);
  process.exit(2);
} catch { /* порт свободен — так и нужно */ }

const dir = mkdtempSync(join(tmpdir(), 'jt-verify-'));
const child = spawn('dotnet', ['run', '--no-build', '--no-launch-profile', '--project', join(root, 'src/JobTracker.Api'), '--urls', base], {
  cwd: root,
  env: { ...process.env, ConnectionStrings__JobTracker: `Data Source=${join(dir, 'verify.db')}`, Content__ImportOnStartup: 'true' },
  stdio: ['ignore', 'pipe', 'pipe'],
});
let log = '';
child.stdout.on('data', d => { log += d; });
child.stderr.on('data', d => { log += d; });

// останавливаем только своё дерево процессов (dotnet run → JobTracker.Api)
function stop() {
  try {
    if (process.platform === 'win32') execFileSync('taskkill', ['/PID', String(child.pid), '/T', '/F'], { stdio: 'ignore' });
    else child.kill('SIGTERM');
  } catch { /* уже завершился */ }
}

let content = null;
for (let i = 0; i < 60 && !content && child.exitCode === null; i++) {
  try {
    const res = await fetch(base + '/api/content');
    if (res.ok) content = await res.json();
  } catch { await new Promise(r => setTimeout(r, 1000)); }
}
stop();
await new Promise(r => setTimeout(r, 500));
try { rmSync(dir, { recursive: true, force: true }); } catch { /* файл базы ещё занят — останется во временной папке */ }

if (!content) {
  console.error('Приложение не отдало /api/content. Конец журнала:\n' + log.split('\n').slice(-25).join('\n'));
  process.exit(1);
}
const sections = content.sections ?? content;
for (const s of sections) {
  const n = (s.groups ?? []).reduce((acc, g) => acc + (g.questions ?? []).length, 0);
  console.log(`${s.number}. ${s.title} — ${n}`);
}
console.log(`\nПриложение загрузило разделов: ${sections.length}.`);
