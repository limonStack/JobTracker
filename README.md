# JobTracker

Трекер откликов на вакансии: воронка (отклик → HR → тех. собес → оффер), история событий по каждой компании и автоматическая проверка — открыта ли ещё вакансия на сайте.

> Проект в разработке.

## Стек

- .NET 10, ASP.NET Core Minimal API
- EF Core + SQLite
- `BackgroundService` для фоновой проверки вакансий
- xUnit

## Структура

```
src/
  JobTracker.Domain          — сущности и бизнес-правила, без внешних зависимостей
  JobTracker.Application     — интерфейсы и сценарии использования
  JobTracker.Infrastructure  — EF Core, HTTP-проверка вакансий на сайтах
  JobTracker.Api             — HTTP API, фоновые задачи, фронтенд (wwwroot)
tests/
  JobTracker.UnitTests
  JobTracker.IntegrationTests
```

Зависимости направлены внутрь: `Api → Infrastructure → Application → Domain`.

## Запуск

```bash
dotnet run --project src/JobTracker.Api
```
