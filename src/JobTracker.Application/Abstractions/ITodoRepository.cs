using JobTracker.Domain.Todos;

namespace JobTracker.Application.Abstractions;

/// <summary>Дела текущего пользователя.</summary>
public interface ITodoRepository
{
    Task<IReadOnlyList<TodoItem>> ListAsync(CancellationToken ct);
    Task<TodoItem?> GetAsync(Guid id, CancellationToken ct);
    void Add(TodoItem item);
    void Remove(TodoItem item);
}
