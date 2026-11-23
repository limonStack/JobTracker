// Хуки Claude Code: запущенное приложение (в том числе под отладчиком) держит файлы в bin/Debug,
// и Debug-сборка падает. Один механизм из двух половин:
//   node dev-app.mjs before-build  — PreToolUse (Bash|PowerShell): команда собирает Debug → остановить
//                                     приложение этого репозитория и запомнить, что остановили мы;
//   node dev-app.mjs after-turn    — Stop (конец ответа Claude): если останавливали мы — запустить снова
//                                     (`dotnet run`, уже без отладчика) и дождаться, пока поднимется.
// Чужие процессы не трогаем: только JobTracker.Api из src/JobTracker.Api/bin/Debug этого репозитория,
// по имени процесса не останавливаем. Любая ошибка — выход с кодом 0: хук не мешает работе.
import { execFileSync, spawn } from 'node:child_process';
import { existsSync, mkdirSync, openSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const apiProject = join(root, 'src', 'JobTracker.Api');
const debugBin = join(apiProject, 'bin', 'Debug');
const appUrl = 'http://localhost:5196';
const stateDir = join(tmpdir(), 'jobtracker-hooks');

/** Пишет ли команда в bin/Debug (или занимает порт приложения): dotnet build/test/ef/run/watch без Release. */
export function needsStop(command) {
  return String(command).split(/&&|\|\||[;|\n]/).some(segment => {
    const m = segment.trim().match(/^(?:&\s*)?dotnet(?:\.exe)?\s+(build|test|ef|run|watch)\b(.*)$/i);
    if (!m) return false;
    const verb = m[1].toLowerCase(), rest = m[2];
    if (/(^|\s)(-c|--configuration)[\s:=]+release\b|-p:Configuration=Release/i.test(rest)) return false;
    // свой экземпляр на другом порту без сборки (проверки) — не мешает приложению пользователя
    if (verb === 'run' && /--urls/.test(rest)) return false;
    if (verb !== 'run' && /--no-build/.test(rest)) return false;
    return true;
  });
}

function powershell(script) {
  return execFileSync('powershell', ['-NoProfile', '-NonInteractive', '-Command', script], { encoding: 'utf8', windowsHide: true, timeout: 30_000 });
}

/** PID приложения этого репозитория: JobTracker.Api.exe из bin/Debug или dotnet JobTracker.Api.dll оттуда же. */
function findAppPids() {
  const bin = debugBin.replace(/'/g, "''");
  const out = powershell(`$bin = '${bin}'
Get-CimInstance Win32_Process -Filter "Name='JobTracker.Api.exe' OR Name='dotnet.exe'" | Where-Object {
  ($_.ExecutablePath -and $_.ExecutablePath.StartsWith($bin, [StringComparison]::OrdinalIgnoreCase)) -or
  ($_.Name -eq 'dotnet.exe' -and $_.CommandLine -and $_.CommandLine.IndexOf($bin, [StringComparison]::OrdinalIgnoreCase) -ge 0 -and $_.CommandLine -match 'JobTracker\\.Api\\.dll')
} | ForEach-Object { $_.ProcessId }`);
  return out.split(/\s+/).filter(Boolean).map(Number).filter(n => n !== process.pid);
}

function stopPids(pids) {
  powershell(`Stop-Process -Id ${pids.join(',')} -Force -ErrorAction SilentlyContinue
Wait-Process -Id ${pids.join(',')} -Timeout 15 -ErrorAction SilentlyContinue`);
}

async function appResponds() {
  try { await fetch(appUrl + '/api/content', { signal: AbortSignal.timeout(1500) }); return true; } catch { return false; }
}

const stateFile = sessionId => join(stateDir, `${String(sessionId || 'default').replace(/[^\w-]/g, '')}.json`);
const say = message => console.log(JSON.stringify({ systemMessage: message }));

async function beforeBuild(input) {
  const command = input.tool_input?.command ?? '';
  if (!needsStop(command)) return;
  const pids = findAppPids();
  if (!pids.length) return;
  stopPids(pids);
  mkdirSync(stateDir, { recursive: true });
  writeFileSync(stateFile(input.session_id), JSON.stringify({ stoppedAt: new Date().toISOString(), pids }));
  say(`Остановлен JobTracker (PID ${pids.join(', ')}): он держал файлы bin/Debug, а Claude собирает проект. В конце ответа приложение запустится снова — уже без отладчика.`);
}

async function afterTurn(input) {
  const file = stateFile(input.session_id);
  if (!existsSync(file)) return;                 // приложение останавливали не мы — не запускаем
  rmSync(file, { force: true });
  if (await appResponds()) return;               // уже запустили (например, вы из IDE)

  const log = join(stateDir, 'dotnet-run.log');
  // профиль http — как из IDE: Development, порт 5196 и новая вкладка (старую закроет single-tab.js);
  // JOBTRACKER_HOOK_NO_BROWSER=1 — без вкладки, для проверок самого хука
  const args = process.env.JOBTRACKER_HOOK_NO_BROWSER === '1'
    ? ['run', '--project', apiProject, '--no-launch-profile', '--urls', appUrl]
    : ['run', '--project', apiProject, '--launch-profile', 'http'];
  const out = openSync(log, 'w');
  const child = spawn('dotnet', args, {
    cwd: root, detached: true, windowsHide: true, stdio: ['ignore', out, out],
    env: { ...process.env, ASPNETCORE_ENVIRONMENT: process.env.ASPNETCORE_ENVIRONMENT ?? 'Development' },
  });
  child.unref();

  for (let i = 0; i < 90; i++) {
    if (await appResponds()) { say(`JobTracker снова запущен: ${appUrl} (без отладчика).`); return; }
    await new Promise(r => setTimeout(r, 1000));
  }
  say(`JobTracker не поднялся за 90 секунд — возможно, сборка не прошла. Журнал: ${log}`);
}

const isMain = process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href;
if (isMain) {
  try {
    let raw = '';
    for await (const chunk of process.stdin) raw += chunk;
    const input = raw.trim() ? JSON.parse(raw) : {};
    if (process.argv[2] === 'before-build') await beforeBuild(input);
    else if (process.argv[2] === 'after-turn') await afterTurn(input);
  } catch (err) {
    console.error(`dev-app hook: ${err.message}`);   // для отладки хука; на работу не влияет
  }
  process.exit(0);
}
