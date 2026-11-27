// Хуки Claude Code: приложение JobTracker всегда работает с последними правками Claude.
//   node dev-app.mjs turn-start    — UserPromptSubmit: запомнить начало ответа (от него считаются правки);
//   node dev-app.mjs before-build  — PreToolUse (Bash|PowerShell): команда собирает Debug → остановить
//                                     приложение этого репозитория (оно держит файлы bin/Debug);
//   node dev-app.mjs after-turn    — Stop (конец ответа): если за ответ менялись исходники в src/ или контент
//                                     в content/ (или приложение останавливали мы) — остановить старый экземпляр,
//                                     собрать и запустить заново (`dotnet run`), дождаться, пока поднимется.
// Отладчик VS Code нельзя запустить извне, поэтому приложение стартует без него, а отладка подключается
// конфигурацией «Attach JobTracker» из .vscode/launch.json (F5 с ней — подключиться к процессу по имени).
// Правки только в wwwroot перезапуска не требуют: статика отдаётся с no-cache, достаточно обновить страницу.
// Чужие процессы не трогаем: только JobTracker.Api из src/JobTracker.Api/bin/Debug этого репозитория,
// по имени процесса не останавливаем. Любая ошибка — выход с кодом 0: хук не мешает работе.
import { execFileSync, spawn } from 'node:child_process';
import { existsSync, mkdirSync, openSync, readdirSync, readFileSync, rmSync, statSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const apiProject = join(root, 'src', 'JobTracker.Api');
const debugBin = join(apiProject, 'bin', 'Debug');
const appUrl = 'http://localhost:5196';
const stateDir = join(tmpdir(), 'jobtracker-hooks');
const watched = [join(root, 'src'), join(root, 'content')];
const ignoredDirs = new Set(['bin', 'obj', 'node_modules', 'wwwroot', '.vs']);

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

/** Время последнего изменения файла в папках (мс); bin, obj, wwwroot и подобные пропускаются. 0 — файлов нет. */
export function newestChange(dirs, ignore = ignoredDirs) {
  let newest = 0;
  const walk = dir => {
    let entries;
    try { entries = readdirSync(dir, { withFileTypes: true }); } catch { return; }
    for (const e of entries) {
      const path = join(dir, e.name);
      if (e.isDirectory()) { if (!ignore.has(e.name)) walk(path); }
      else if (e.isFile()) { try { newest = Math.max(newest, statSync(path).mtimeMs); } catch { /* файл удалён на ходу */ } }
    }
  };
  dirs.forEach(walk);
  return newest;
}

/** Перезапускать ли приложение в конце ответа: правки после начала ответа или его останавливали мы. */
export function shouldRestart({ changedAt, turnStart, stoppedByUs }) {
  return Boolean(stoppedByUs) || (turnStart > 0 && changedAt > turnStart);
}

function powershell(script) {
  return execFileSync('powershell', ['-NoProfile', '-NonInteractive', '-Command', script], { encoding: 'utf8', windowsHide: true, timeout: 30_000 });
}

/** Приложение этого репозитория: JobTracker.Api.exe из bin/Debug или dotnet JobTracker.Api.dll оттуда же; с именем родителя. */
export function findApps() {
  const bin = debugBin.replace(/'/g, "''");
  const out = powershell(`$bin = '${bin}'
Get-CimInstance Win32_Process -Filter "Name='JobTracker.Api.exe' OR Name='dotnet.exe'" | Where-Object {
  ($_.ExecutablePath -and $_.ExecutablePath.StartsWith($bin, [StringComparison]::OrdinalIgnoreCase)) -or
  ($_.Name -eq 'dotnet.exe' -and $_.CommandLine -and $_.CommandLine.IndexOf($bin, [StringComparison]::OrdinalIgnoreCase) -ge 0 -and $_.CommandLine -match 'JobTracker\\.Api\\.dll')
} | ForEach-Object {
  $parent = Get-CimInstance Win32_Process -Filter "ProcessId=$($_.ParentProcessId)"
  "$($_.ProcessId)|$($parent.Name)"
}`);
  return out.split(/\r?\n/).filter(Boolean).map(line => {
    const [pid, parent] = line.trim().split('|');
    return { pid: Number(pid), parent };
  }).filter(a => a.pid && a.pid !== process.pid);
}

function stopPids(pids) {
  powershell(`Stop-Process -Id ${pids.join(',')} -Force -ErrorAction SilentlyContinue
Wait-Process -Id ${pids.join(',')} -Timeout 15 -ErrorAction SilentlyContinue`);
}

async function appResponds() {
  try { await fetch(appUrl + '/api/content', { signal: AbortSignal.timeout(1500) }); return true; } catch { return false; }
}

const session = id => String(id || 'default').replace(/[^\w-]/g, '');
const stateFile = id => join(stateDir, `${session(id)}.json`);
const turnFile = id => join(stateDir, `${session(id)}.turn`);
const say = message => console.log(JSON.stringify({ systemMessage: message }));
const attachHint = 'Отладка: F5 с конфигурацией «Attach JobTracker».';

function turnStart(input) {
  mkdirSync(stateDir, { recursive: true });
  writeFileSync(turnFile(input.session_id), String(Date.now()));
}

async function beforeBuild(input) {
  const command = input.tool_input?.command ?? '';
  if (!needsStop(command)) return;
  const apps = findApps();
  if (!apps.length) return;
  const pids = apps.map(a => a.pid);
  stopPids(pids);
  mkdirSync(stateDir, { recursive: true });
  writeFileSync(stateFile(input.session_id), JSON.stringify({ stoppedAt: new Date().toISOString(), pids }));
  say(`Остановлен JobTracker (PID ${pids.join(', ')}): он держал файлы bin/Debug, а Claude собирает проект. В конце ответа приложение запустится с правками.`);
}

async function afterTurn(input) {
  const state = stateFile(input.session_id), turn = turnFile(input.session_id);
  const stoppedByUs = existsSync(state);
  let started = 0;
  try { started = Number(readFileSync(turn, 'utf8')) || 0; } catch { /* хук начала ответа ещё не срабатывал */ }
  rmSync(state, { force: true });
  rmSync(turn, { force: true });

  const changedAt = started ? newestChange(watched) : 0;
  if (!shouldRestart({ changedAt, turnStart: started, stoppedByUs })) return;

  // старый экземпляр работает без правок — останавливаем (в том числе под отладчиком: сессия отладки завершится)
  const running = findApps();
  if (running.length) stopPids(running.map(a => a.pid));
  if (await appResponds()) {
    say(`Порт 5196 занят не приложением из bin/Debug этого репозитория — JobTracker с правками не запущен.`);
    return;
  }

  const log = join(stateDir, 'dotnet-run.log');
  // Приложение уже работало — вкладка открыта: новую не открываем (вкладку с историей переходов
  // single-tab.js закрыть не может и показал бы заглушку), достаточно обновить страницу.
  // Не работало — профиль http, как из IDE: Development, порт 5196 и новая вкладка.
  // JOBTRACKER_HOOK_NO_BROWSER=1 — без вкладки, для проверок самого хука
  const hadTab = running.length > 0 || stoppedByUs;
  const args = process.env.JOBTRACKER_HOOK_NO_BROWSER === '1' || hadTab
    ? ['run', '--project', apiProject, '--no-launch-profile', '--urls', appUrl]
    : ['run', '--project', apiProject, '--launch-profile', 'http'];
  mkdirSync(stateDir, { recursive: true });
  const out = openSync(log, 'w');
  const child = spawn('dotnet', args, {
    cwd: root, detached: true, windowsHide: true, stdio: ['ignore', out, out],
    env: { ...process.env, ASPNETCORE_ENVIRONMENT: process.env.ASPNETCORE_ENVIRONMENT ?? 'Development' },
  });
  child.unref();

  for (let i = 0; i < 100; i++) {
    if (await appResponds()) {
      const why = changedAt > started ? 'с правками этого ответа' : 'после сборки';
      say(`JobTracker перезапущен ${why}: ${appUrl}${hadTab ? ' — обновите открытую вкладку (F5)' : ''}. ${attachHint}`);
      return;
    }
    await new Promise(r => setTimeout(r, 1000));
  }
  say(`JobTracker не поднялся за 100 секунд — возможно, сборка не прошла. Журнал: ${log}`);
}

const isMain = process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href;
if (isMain) {
  try {
    let raw = '';
    for await (const chunk of process.stdin) raw += chunk;
    const input = raw.trim() ? JSON.parse(raw) : {};
    if (process.argv[2] === 'turn-start') turnStart(input);
    else if (process.argv[2] === 'before-build') await beforeBuild(input);
    else if (process.argv[2] === 'after-turn') await afterTurn(input);
  } catch (err) {
    console.error(`dev-app hook: ${err.message}`);   // для отладки хука; на работу не влияет
  }
  process.exit(0);
}
