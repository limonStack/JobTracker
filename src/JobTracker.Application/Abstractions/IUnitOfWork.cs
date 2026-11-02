namespace JobTracker.Application.Abstractions;

/// <summary>Фиксирует все изменения одной операции разом; после сохранения рассылает доменные события.</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct = default);
}
