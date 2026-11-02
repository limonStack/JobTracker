using JobTracker.Domain.Common;

namespace JobTracker.Application.Abstractions;

/// <summary>Подписчик на доменное событие (Observer): реакция на событие не зашита в того, кто его поднял.</summary>
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken ct);
}
