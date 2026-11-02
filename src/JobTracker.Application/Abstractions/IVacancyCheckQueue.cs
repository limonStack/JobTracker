namespace JobTracker.Application.Abstractions;

/// <summary>
/// Очередь «проверить все вакансии» (Producer–Consumer): кнопка в интерфейсе и таймер кладут запрос,
/// фоновый воркер забирает и выполняет. HTTP-запрос не ждёт, пока обойдутся все сайты.
/// </summary>
public interface IVacancyCheckQueue
{
    /// <summary>False — проверка уже стоит в очереди, второй раз не ставим.</summary>
    bool RequestCheckAll();

    /// <summary>В очереди есть проверка, которую воркер ещё не взял.</summary>
    bool HasPending { get; }

    IAsyncEnumerable<bool> ReadAllAsync(CancellationToken ct);
}
