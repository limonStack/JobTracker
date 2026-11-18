using JobTracker.Application.Abstractions;
using JobTracker.Domain.Todos;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.Infrastructure.Persistence;

internal sealed class TodoRepository(JobTrackerDbContext db) : ITodoRepository
{
    public async Task<IReadOnlyList<TodoItem>> ListAsync(CancellationToken ct) =>
        await db.Set<TodoItem>().AsNoTracking().ToListAsync(ct);

    public Task<TodoItem?> GetAsync(Guid id, CancellationToken ct) =>
        db.Set<TodoItem>().FirstOrDefaultAsync(x => x.Id == id, ct);

    public void Add(TodoItem item) => db.Add(item);

    public void Remove(TodoItem item) => db.Remove(item);
}
