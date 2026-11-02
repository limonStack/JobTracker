using System.Threading.Channels;
using JobTracker.Application.Abstractions;

namespace JobTracker.Infrastructure.VacancyChecking;

/// <summary>
/// Очередь на Channel ёмкостью 1: пока проверка ждёт в очереди, повторные нажатия «Проверить все»
/// не плодят одинаковые задачи. Пока идёт обход, одна следующая проверка поставиться может.
/// </summary>
public sealed class ChannelVacancyCheckQueue : IVacancyCheckQueue
{
    private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true });

    public bool RequestCheckAll() => _channel.Writer.TryWrite(true);

    public bool HasPending => _channel.Reader.Count > 0;

    public IAsyncEnumerable<bool> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}
