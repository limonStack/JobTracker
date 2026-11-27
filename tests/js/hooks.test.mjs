// Хук Claude Code .claude/hooks/vacancy-link.mjs: подсказка про /interview-prep по ссылке на вакансию.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { findVacancy, findSection, hint } from '../../.claude/hooks/vacancy-link.mjs';

const files = ['<main><section data-kind="company"><h1 id="25-acme">25. ACME</h1></section></main>'];

test('находит вакансию DOU и слаг компании', () => {
  assert.deepEqual(findVacancy('глянь https://jobs.dou.ua/companies/acme/vacancies/100003/?from=rs пожалуйста'),
    { url: 'https://jobs.dou.ua/companies/acme/vacancies/100003/?from=rs', companySlug: 'acme' });
});

test('находит Djinni, Work.ua и robota.ua без слага компании', () => {
  assert.equal(findVacancy('https://djinni.co/jobs/712345-senior-net/').companySlug, null);
  assert.ok(findVacancy('https://www.work.ua/jobs/6123456/'));
  assert.ok(findVacancy('https://robota.ua/company123/vacancy987654'));
});

test('обычные ссылки — не вакансия', () => {
  assert.equal(findVacancy('https://jobs.dou.ua/companies/acme/'), null);
  assert.equal(findVacancy('https://github.com/x/y'), null);
  assert.equal(hint('как дела?', files), null);
});

test('skill уже вызван — подсказки нет', () => {
  assert.equal(hint('/interview-prep https://jobs.dou.ua/companies/acme/vacancies/1/', files), null);
});

test('подсказка называет готовый раздел компании', () => {
  const text = hint('https://jobs.dou.ua/companies/acme/vacancies/1/', files);
  assert.match(text, /interview-prep/);
  assert.match(text, /«25\. ACME» \(#25-acme\)/);
});

test('раздела нет — подсказка без него', () => {
  const text = hint('https://jobs.dou.ua/companies/fabrikam/vacancies/1/', files);
  assert.match(text, /interview-prep/);
  assert.doesNotMatch(text, /уже есть/);
  assert.equal(findSection('fabrikam', files), null);
});

// .claude/hooks/dev-app.mjs: какие команды мешают запущенному приложению (пишут в bin/Debug или занимают его порт)
import { needsStop, isDebuggerParent } from '../../.claude/hooks/dev-app.mjs';

test('под отладчиком IDE — не перезапускать самим, обычный запуск — перезапускать', () => {
  for (const parent of ['vsdbg.exe', 'vsdbg-ui.exe', 'JetBrains.Debugger.Worker.exe', 'devenv.exe', 'msvsmon.exe', 'VsDebugConsole.exe'])
    assert.equal(isDebuggerParent(parent), true, parent);
  for (const parent of ['dotnet.exe', 'powershell.exe', 'Code.exe', 'explorer.exe', '', undefined])
    assert.equal(isDebuggerParent(parent), false, String(parent));
});

test('Debug-сборка, тесты, миграции и запуск — останавливать приложение', () => {
  for (const cmd of ['dotnet build', 'dotnet test --filter "FullyQualifiedName~X"', 'dotnet ef migrations add X --project a',
    'dotnet run --project src/JobTracker.Api', 'cd e:/x && dotnet build 2>&1 | tail', 'dotnet tool restore; dotnet build -c Debug'])
    assert.equal(needsStop(cmd), true, cmd);
});

test('Release, --no-build, свой экземпляр на другом порту и не-dotnet — не трогать', () => {
  for (const cmd of ['dotnet build -c Release', 'dotnet build --configuration Release', 'dotnet ef migrations add X --configuration Release',
    'dotnet test -c Release --no-build', 'dotnet test --no-build', 'dotnet run -c Release --no-build --urls http://localhost:5298',
    'dotnet run --no-build --no-launch-profile --urls http://localhost:5299', 'echo "dotnet build"', 'npm test', 'git status', 'dotnet tool restore'])
    assert.equal(needsStop(cmd), false, cmd);
});

// .claude/hooks/readme-check.mjs: какой коммит стоит отразить в README
import { readmeReasons, readmeMessage } from '../../.claude/hooks/readme-check.mjs';

const A = path => ({ status: 'A', path }), M = path => ({ status: 'M', path });

test('README: новые модуль API, сайт вакансий, фоновая задача, модуль фронтенда, модуль домена', () => {
  const reasons = readmeReasons([
    A('src/JobTracker.Api/Endpoints/PrepEndpoints.cs'),
    A('src/JobTracker.Infrastructure/VacancyChecking/DjinniChecker.cs'),
    A('src/JobTracker.Api/BackgroundJobs/PrepWorker.cs'),
    A('src/JobTracker.Api/wwwroot/js/prep.js'),
    A('src/JobTracker.Domain/Prep/PrepJob.cs'),
  ], { existedBefore: dir => !dir.endsWith('/Prep') });
  assert.equal(reasons.length, 5);
  assert.match(reasons.join('\n'), /PrepEndpoints\.cs[\s\S]*DjinniChecker\.cs[\s\S]*PrepWorker\.cs[\s\S]*prep\.js[\s\S]*домена: Prep/);
});

test('README: пакеты, CI и большой коммит — тоже повод', () => {
  assert.deepEqual(readmeReasons([M('src/JobTracker.Api/JobTracker.Api.csproj')], { packagesChanged: true }), ['изменились NuGet-пакеты (стек)']);
  assert.deepEqual(readmeReasons([M('.github/workflows/ci.yml')]), ['изменился CI']);
  const many = Array.from({ length: 12 }, (_, i) => M(`src/JobTracker.Application/X${i}.cs`));
  assert.match(readmeReasons(many)[0], /12 файлов/);
});

test('README: мелкие правки, миграции, тесты, файл в существующем модуле и коммит с README — молчим', () => {
  assert.deepEqual(readmeReasons([M('src/JobTracker.Api/wwwroot/js/knowledge.js'), A('tests/js/x.test.mjs')]), []);
  assert.deepEqual(readmeReasons([A('src/JobTracker.Domain/Knowledge/SectionKind.cs')]), []);
  assert.deepEqual(readmeReasons(Array.from({ length: 20 }, (_, i) => A(`src/JobTracker.Infrastructure/Persistence/Migrations/M${i}.cs`))), []);
  assert.deepEqual(readmeReasons([A('src/JobTracker.Api/Endpoints/PrepEndpoints.cs'), M('README.md')]), []);
  assert.deepEqual(readmeReasons([A('src/JobTracker.Infrastructure/VacancyChecking/HtmlVacancyChecker.cs')]), []);
  assert.equal(readmeMessage('abc', 'x', []), null);
});
