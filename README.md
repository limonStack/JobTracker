# JobTracker

[![CI](https://github.com/limonStack/JobTracker/actions/workflows/ci.yml/badge.svg)](https://github.com/limonStack/JobTracker/actions/workflows/ci.yml)

Трекер откликов на вакансии: воронка (отклик → HR → тех. собес → оффер), история событий по каждой компании
и **автоматическая проверка — открыта ли ещё вакансия на сайте**. Сейчас поддерживается [DOU](https://jobs.dou.ua),
архитектура рассчитана на добавление других сайтов.

![Скриншот](docs/screenshot.png)

## Возможности

- Воронка откликов с фильтрами по этапам и статистикой «сколько ответили / дошли до HR / тех. собеса»
- История событий по каждому отклику; смена этапа сама пишет событие
- Проверка вакансий на сайте:
  - в фоне раз в сутки и при запуске приложения;
  - по кнопке «Проверить все» или «Проверить сейчас» для одной вакансии;
  - закрытые вакансии помечаются 🔒 и попадают в фильтр «закрыты»
- Импорт из старого трекера (localStorage), экспорт в CSV и JSON

## Стек

- .NET 10, ASP.NET Core Minimal API
- EF Core 10 + SQLite
- `BackgroundService`, `PeriodicTimer`, `System.Threading.Channels`
- `IHttpClientFactory` + `Microsoft.Extensions.Http.Resilience` (Polly)
- AngleSharp — разбор HTML
- xUnit, `WebApplicationFactory`; GitHub Actions
- Фронтенд — ванильный JS без сборки

## Запуск

Нужен [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
dotnet run --project src/JobTracker.Api
```

Откройте http://localhost:5196. База `jobtracker.db` создаётся рядом с проектом при первом запуске, миграции применяются автоматически.

Тесты:

```bash
dotnet test
```

### Настройки

`src/JobTracker.Api/appsettings.json`:

```jsonc
"VacancyChecks": {
  "Interval": "1.00:00:00",          // как часто проверять все вакансии
  "DelayBetweenRequests": "00:00:02", // пауза между запросами к сайту
  "CheckOnStartup": true              // проверить сразу при запуске
}
```

## Архитектура

```
src/
  JobTracker.Domain          — сущности и бизнес-правила, без внешних зависимостей
  JobTracker.Application     — сценарии использования и интерфейсы (порты)
  JobTracker.Infrastructure  — EF Core/SQLite, проверка вакансий по HTTP, очередь
  JobTracker.Api             — Minimal API, фоновый воркер, фронтенд (wwwroot)
tests/
  JobTracker.UnitTests        — домен, DouChecker на сохранённых страницах
  JobTracker.IntegrationTests — API целиком через WebApplicationFactory
```

Зависимости направлены внутрь: `Api → Infrastructure → Application → Domain`.

### Как работает проверка вакансии

```
таймер (раз в сутки) ─┐
кнопка «Проверить все» ┴─► Channel ─► VacancyCheckWorker ─► VacancyCheckService
                                        (BackgroundService)      │
                                                                 ▼
                               VacancyCheckerResolver ─► DouChecker ─► jobs.dou.ua
                                                                 │
                              JobApplication.RecordCheck ◄───────┘
                                     │ вакансия закрылась
                                     ▼
                              VacancyClosedEvent ─► подписчики (лог; дальше — Telegram)
```

На DOU у закрытой вакансии к заголовку дописано «(вакансія неактивна)», а блок описания получает класс
`__inactive`. `DouChecker` смотрит на оба признака. Если на странице нет ни того, ни другого (капча, новая
разметка), результат — «не удалось проверить», а не «открыта».

### Паттерны и решения

| Что | Где | Зачем |
|---|---|---|
| Rich Domain Model, Aggregate | `JobApplication` | правила воронки внутри сущности: этап меняется только через `MoveTo` |
| Value Object | `VacancyUrl` | ссылка всегда валидная и нормализованная |
| Domain Events / Observer | `VacancyClosedEvent`, `IDomainEventHandler<T>` | реакции на закрытие добавляются без изменения проверки |
| Strategy | `IVacancyChecker` → `DouChecker` | своя проверка для каждого сайта |
| Template Method | `HtmlVacancyChecker` | общий скелет «загрузить → разобрать → решить» |
| Open/Closed | `VacancyCheckerResolver` | новый сайт = новый класс + строка регистрации |
| Result pattern | `VacancyCheckResult` | сбой сети — ожидаемый исход, а не исключение |
| Repository + Unit of Work | `IJobApplicationRepository`, `JobTrackerDbContext` | события рассылаются только после успешного `SaveChanges` |
| Producer–Consumer | `ChannelVacancyCheckQueue` + `VacancyCheckWorker` | HTTP-запрос не ждёт обхода всех сайтов |
| Options pattern | `VacancyCheckOptions` | настройки с валидацией при старте |
| Decorator (DelegatingHandler) | `AddStandardResilienceHandler` | повторы и таймауты поверх HttpClient |
| DI lifetimes | `IServiceScopeFactory` в воркере | singleton-воркер не держит scoped `DbContext` |

## Перенос данных из старого трекера

1. В старом трекере нажмите F12 → Console и выполните
   `copy(localStorage.getItem('net-interview-jobs-v1'))`.
2. В JobTracker нажмите «⬆ Импорт» и вставьте скопированное.

Импорт идемпотентный: повторный запуск не создаёт дублей.

## Планы

- Проверка вакансий на Djinni и Work.ua
- Уведомление в Telegram о закрытии вакансии (ещё один подписчик `VacancyClosedEvent`)
- Запуск как Windows Service
