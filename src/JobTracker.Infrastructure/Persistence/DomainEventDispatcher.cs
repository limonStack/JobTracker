using System.Reflection;
using JobTracker.Application.Abstractions;
using JobTracker.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

namespace JobTracker.Infrastructure.Persistence;

/// <summary>Находит в DI все IDomainEventHandler&lt;T&gt; для типа события и вызывает их по очереди.</summary>
public sealed class DomainEventDispatcher(IServiceProvider services)
{
    private static readonly MethodInfo DispatchOneMethod =
        typeof(DomainEventDispatcher).GetMethod(nameof(DispatchOneAsync), BindingFlags.NonPublic | BindingFlags.Instance)!;

    public async Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken ct)
    {
        foreach (var domainEvent in events)
        {
            var dispatch = DispatchOneMethod.MakeGenericMethod(domainEvent.GetType());
            await (Task)dispatch.Invoke(this, [domainEvent, ct])!;
        }
    }

    private async Task DispatchOneAsync<TEvent>(TEvent domainEvent, CancellationToken ct) where TEvent : IDomainEvent
    {
        foreach (var handler in services.GetServices<IDomainEventHandler<TEvent>>())
            await handler.HandleAsync(domainEvent, ct);
    }
}
