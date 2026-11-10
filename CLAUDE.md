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

- **Domain** — богатая модель без внешних зависимостей. Модули: `Applications` (отклики; `JobApplication` — агрегат, этап меняется только через `MoveTo`, `VacancyUrl` — value object), `Knowledge` (общий контент: разделы, вопросы), `Study` (отметки «изучено», треки, настройки), `Notes` (идеи, выделения). Нарушение правил — `DomainException`, его `DomainExceptionHandler` превращает в ProblemDetails.
- **Application** — сервисы сценариев (`JobApplicationService`, `StudyService`, `NotesService`, `ContentService`, `VacancyCheckService`, импорт из старого сайта) и порты в `Abstractions/`.
- **Infrastructure** — EF Core/SQLite, разбор `content/answers.html` (AngleSharp), HTTP-проверяльщики вакансий, очередь на `Channel`.
- **Api** — Minimal API (`Endpoints/*Endpoints.cs`, по файлу на модуль, подключаются в `Program.cs`), фоновый `VacancyCheckWorker`, фронтенд в `wwwroot`.

### Разделение данных по пользователям (важно при добавлении сущностей)

Пользовательская сущность помечается `IUserOwned` — и всё. `JobTrackerDbContext` сам добавляет теневое свойство `UserId` с индексом, именованный глобальный фильтр `"ByUser"` по `ICurrentUser` и проставляет владельца новым записям в `SaveChanges`. В домене `UserId` не хранится. Сейчас `ICurrentUser` = `LocalCurrentUser` (один локальный пользователь); авторизация заменит только эту реализацию. Схема уже рассчитана на облако и многих пользователей — новые пользовательские данные хранить в БД, не в браузере. «Сегодня» считается через `IUserClock` (часовой пояс пользователя).

### Unit of Work и доменные события

`JobTrackerDbContext` реализует `IUnitOfWork`: `IUnitOfWork.SaveChangesAsync` собирает события сущностей (`Entity.DomainEvents`), сохраняет и **только после коммита** рассылает их через `DomainEventDispatcher` всем `IDomainEventHandler<T>` из DI. Новая реакция (например, Telegram на `VacancyClosedEvent`) = новый обработчик + регистрация.

### Проверка вакансий

Таймер / «Проверить все» → `ChannelVacancyCheckQueue` → `VacancyCheckWorker` (singleton, scope через `IServiceScopeFactory`) → `VacancyCheckService` → `VacancyCheckerResolver` выбирает `IVacancyChecker` по `CanCheck(url)`. Проверяльщики наследуют Template Method `HtmlVacancyChecker` (загрузить → `AnalyzeResponse` → разобрать HTML). Новый сайт = новый класс + регистрация `AddHttpClient` в `Infrastructure/DependencyInjection.cs`. Сбой сети/неизвестная разметка — `VacancyCheckResult` «не удалось проверить», не исключение и не «открыта». Тесты проверяльщиков работают на сохранённых страницах `tests/JobTracker.UnitTests/VacancyChecking/Fixtures/`.

### Контент (тексты ответов)

`content/answers.html` разбирается `AnswersHtmlParser` и импортируется в базу при старте, только если изменился хеш файлов (`ContentService.ImportIfChangedAsync`). Личные разделы — `content/private/` (вне git; без папки приложение работает). `GET /api/content` отдаётся с ETag.

### Фронтенд (ванильный JS, ES-модули, без сборки)

Офлайн-first PWA. `js/main.js` — точка входа; `js/db.js` — IndexedDB; `js/sync.js` — снимок `GET /api/sync` + очередь изменений; `js/store.js` — данные пользователя (изменения уходят в API разницей); `js/render.js` — меню и тексты из `/api/content`; `js/knowledge.js` — логика базы знаний; `app.js` — панель откликов; `sw.js` — кэш оболочки «сначала сеть».

Контракт синхронизации, который нельзя ломать: правка сразу применяется к локальному снимку и встаёт в очередь; запросы очереди **идемпотентны** — id новых записей генерирует браузер (UUID v7) и шлёт `PUT`-ом, дата действия передаётся явно. Значит, новые API-эндпоинты записи должны принимать id и дату от клиента и выдерживать повтор. Новые пользовательские данные нужно добавить и в снимок `SyncEndpoints`.

Статика отдаётся с `Cache-Control: no-cache`, API и страница — с одного адреса (без CORS).

### Тесты

- Unit: домен, парсер контента (`RealContentTests` — на настоящем `content/answers.html`), проверяльщики на фикстурах, изоляция пользователей.
- Integration: `JobTrackerApiFactory` (`WebApplicationFactory<Program>`) — временная SQLite-база на каждый экземпляр, `FakeVacancyChecker` вместо сети, импорт контента по умолчанию выключен (`ImportContent = true`, если нужен).

## Публичный репозиторий: секреты и личные данные

- Секреты — только `dotnet user-secrets` / переменные окружения (`ConnectionStrings__JobTracker`).
- Не коммитить: `*.db*`, `content/private/`, `backups/`, `.env*`, `appsettings.*.local.json`, ключи. Хук `.githooks/pre-commit` (подключение: `git config core.hooksPath .githooks`) это проверяет — не обходить `--no-verify`.
- Папку старого проекта 300questions и его localStorage не изменять — только читать; для экспериментов работать с копией.
