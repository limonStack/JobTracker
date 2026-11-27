# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Язык проекта — русский: README, комментарии в коде, сообщения коммитов, тексты интерфейса. Идентификаторы — по-английски.

## Команды

```bash
dotnet run --project src/JobTracker.Api        # http://localhost:5196; база jobtracker.db создаётся, миграции применяются при старте
dotnet build                                    # TreatWarningsAsErrors + AnalysisLevel latest-recommended — любое предупреждение ломает сборку
dotnet test                                     # unit + integration
dotnet test --filter "FullyQualifiedName~DouCheckerTests"          # один класс
dotnet test --filter "FullyQualifiedName~JobApplicationTests.MoveTo" # один тест / группа
npm test                                        # фронтенд: node --test tests/js/*.test.mjs, без зависимостей
node --test tests/js/sync.test.mjs              # один файл фронтенд-тестов

# миграции (dotnet-ef — локальный инструмент, dotnet tool restore)
dotnet ef migrations add <Name> --project src/JobTracker.Infrastructure --startup-project src/JobTracker.Api --output-dir Persistence/Migrations
```

CI (`.github/workflows/ci.yml`): gitleaks по всей истории, `dotnet build -c Release`, `dotnet test`, `npm test`.

## Архитектура

Clean Architecture, зависимости внутрь: `Api → Infrastructure → Application → Domain`.

- **Domain** — богатая модель без внешних зависимостей. Модули: `Applications` (отклики; `JobApplication` — агрегат, этап меняется только через `MoveTo`, `VacancyUrl` — value object), `Knowledge` (общий контент: разделы, вопросы), `Study` (отметки «изучено», треки, настройки), `Notes` (заметки — в коде и API `Idea` / `/api/ideas`, статус в интерфейсе не используется; выделения). Нарушение правил — `DomainException`, его `DomainExceptionHandler` превращает в ProblemDetails.
- **Application** — сервисы сценариев (`JobApplicationService`, `StudyService`, `NotesService`, `ContentService`, `VacancyCheckService`, импорт из старого сайта) и порты в `Abstractions/`.
- **Infrastructure** — EF Core/SQLite, разбор `content/answers.html` (AngleSharp), HTTP-проверяльщики вакансий, очередь на `Channel`.
- **Api** — Minimal API (`Endpoints/*Endpoints.cs`, по файлу на модуль, подключаются в `Program.cs`), фоновый `VacancyCheckWorker`, фронтенд в `wwwroot`.

### Разделение данных по пользователям (важно при добавлении сущностей)

Пользовательская сущность помечается `IUserOwned` — и всё. `JobTrackerDbContext` сам добавляет теневое свойство `UserId` с индексом, именованный глобальный фильтр `"ByUser"` по `ICurrentUser` и проставляет владельца новым записям в `SaveChanges`. В домене `UserId` не хранится. Сейчас `ICurrentUser` = `LocalCurrentUser` (один локальный пользователь); авторизация заменит только эту реализацию. Схема уже рассчитана на облако и многих пользователей — новые пользовательские данные хранить в БД, не в браузере. «Сегодня» считается через `IUserClock` (часовой пояс пользователя).

### Unit of Work и доменные события

`JobTrackerDbContext` реализует `IUnitOfWork`: `IUnitOfWork.SaveChangesAsync` собирает события сущностей (`Entity.DomainEvents`), сохраняет и **только после коммита** рассылает их через `DomainEventDispatcher` всем `IDomainEventHandler<T>` из DI. Новая реакция (например, Telegram на `VacancyClosedEvent`) = новый обработчик + регистрация.

### Проверка вакансий

Таймер / «Проверить все» → `ChannelVacancyCheckQueue` → `VacancyCheckWorker` (singleton, scope через `IServiceScopeFactory`) → `VacancyCheckService` → `VacancyCheckerResolver` выбирает `IVacancyChecker` по `CanCheck(url)`. Проверяльщики наследуют Template Method `HtmlVacancyChecker` (загрузить → `AnalyzeResponse` → разобрать HTML). Новый сайт = новый класс + строка `services.AddVacancyChecker<TChecker>()` в `Infrastructure/DependencyInjection.cs` (не голый `AddHttpClient`: помощник ещё регистрирует класс как `IVacancyChecker`, иначе резолвер его не увидит, и добавляет браузерные заголовки и Polly). Сбой сети/неизвестная разметка — `VacancyCheckResult` «не удалось проверить», не исключение и не «открыта». Тесты проверяльщиков работают на сохранённых страницах `tests/JobTracker.UnitTests/VacancyChecking/Fixtures/`.

### Контент (тексты ответов)

Хуки Claude Code — в `.claude/settings.json`, скрипты в `.claude/hooks/` (тесты — `tests/js/hooks.test.mjs`): `vacancy-link.mjs` (UserPromptSubmit) подсказывает `/interview-prep`, если в сообщении ссылка на вакансию; `dev-app.mjs` запоминает начало ответа (UserPromptSubmit), перед Debug-сборкой (`dotnet build/test/ef/run` без Release) останавливает приложение этого репозитория из `bin/Debug` (PreToolUse), а в конце ответа (Stop) перезапускает его через `dotnet run`, если за ответ менялись `src/` (кроме `wwwroot`) или `content/` либо он его останавливал; отладчик подключается конфигурацией «Attach JobTracker» из `.vscode/launch.json`; `readme-check.mjs` (PostToolUse) после `git commit` с новой возможностью (модуль API или домена, сайт вакансий, фоновая задача, модуль фронтенда, пакеты, CI, 12+ файлов в `src/`) без правки README просит проверить README (`--ref <sha>` — проверить коммит вручную).

Подготовка к вакансии — skill `.claude/skills/interview-prep` (`/interview-prep`): новые темы по пробелам дописываются в конец тематических разделов (`append-questions.mjs`; в публичный — без названий компаний), а раздел компании — без `h3`: «Вероятные вопросы на интервью» (`<details class="qa">`), таблица ссылок «Карта подготовки», домен и вопросы к компании; его скрипты `check-content.mjs` (ссылки, в том числе из публичного файла в личный, нумерация, метки) и `list-questions.mjs` (оглавление обоих файлов) полезны при любой правке контента.

`content/answers.html` разбирается `AnswersHtmlParser` и импортируется в базу при старте, только если изменился хеш файлов (`ContentService.ImportIfChangedAsync`) и включён `Content:ImportOnStartup`. Файлы читаются из копии рядом с приложением (`bin/.../content`, копируется сборкой), поэтому правка `content/` вступает в силу только после пересборки. Личные разделы — `content/private/` (вне git; без папки приложение работает). Раздел под вакансию компании — `<section data-kind="company">` (`SectionKind.Company`): в меню он в отдельном блоке «🏢 Компании» и не входит в общий прогресс; такие разделы только в личном файле. `GET /api/content` отдаётся с ETag.

### Фронтенд (ванильный JS, ES-модули, без сборки)

Офлайн-first PWA. `js/main.js` — точка входа; `js/db.js` — IndexedDB; `js/sync.js` — снимок `GET /api/sync` + очередь изменений; `js/store.js` — данные пользователя (изменения уходят в API разницей); `js/render.js` — меню и тексты из `/api/content`; `js/knowledge.js` — логика базы знаний (переходы по ссылкам — `pushState`, «назад» возвращает прокрутку); `js/route.js` — проход по «Карте подготовки» раздела компании; `app.js` — панель откликов; `sw.js` — кэш оболочки «сначала сеть».

Контракт синхронизации, который нельзя ломать: правка сразу применяется к локальному снимку и встаёт в очередь; запросы очереди **идемпотентны** — id новых записей генерирует браузер (UUID v7) и шлёт `PUT`-ом, дата действия передаётся явно. Значит, новые API-эндпоинты записи должны принимать id и дату от клиента и выдерживать повтор: образец — `PUT /{id}` (upsert), а не соседние `POST` (`/api/applications`, `/events`, `/ideas`, `/highlights` создают id на сервере, фронтенд их не использует). Новые пользовательские данные нужно добавить и в снимок `SyncEndpoints`.

Статика отдаётся с `Cache-Control: no-cache`, API и страница — с одного адреса (без CORS).

### Тесты

- Unit: домен, парсер контента (`RealContentTests` — на настоящем `content/answers.html`), проверяльщики на фикстурах, изоляция пользователей.
- Integration: `JobTrackerApiFactory` (`WebApplicationFactory<Program>`) — временная SQLite-база на каждый экземпляр, `FakeVacancyChecker` вместо сети, импорт контента по умолчанию выключен (`ImportContent = true`, если нужен).

## Публичный репозиторий: секреты и личные данные

- Секреты — только `dotnet user-secrets` / переменные окружения (`ConnectionStrings__JobTracker`).
- Не коммитить: `*.db*`, `content/private/`, `backups/`, `.env*`, `appsettings.*.local.json`, ключи. Хук `.githooks/pre-commit` (подключение: `git config core.hooksPath .githooks`) это проверяет — не обходить `--no-verify`.
- Папку старого проекта 300questions и его localStorage не изменять — только читать; для экспериментов работать с копией.
