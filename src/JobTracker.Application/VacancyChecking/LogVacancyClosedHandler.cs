using JobTracker.Application.Abstractions;
using JobTracker.Domain.Applications;
using Microsoft.Extensions.Logging;

namespace JobTracker.Application.VacancyChecking;

/// <summary>
/// Подписчик на закрытие вакансии. Сейчас только пишет в лог; уведомление в Telegram
/// будет ещё одним подписчиком рядом — проверку вакансий для этого менять не придётся.
/// </summary>
public sealed partial class LogVacancyClosedHandler(ILogger<LogVacancyClosedHandler> logger)
    : IDomainEventHandler<VacancyClosedEvent>
{
    public Task HandleAsync(VacancyClosedEvent domainEvent, CancellationToken ct)
    {
        LogClosed(domainEvent.Company, domainEvent.ClosedAt);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Вакансия {Company} закрылась ({ClosedAt:dd.MM.yyyy})")]
    private partial void LogClosed(string company, DateTimeOffset closedAt);
}
