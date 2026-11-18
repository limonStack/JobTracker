using JobTracker.Application.Abstractions;
using JobTracker.Domain.Todos;

namespace JobTracker.Application.Todos;

public sealed record TodoDto(Guid Id, string Title, DateOnly PlannedOn, DateOnly? DoneOn)
{
    public static TodoDto From(TodoItem x) => new(x.Id, x.Title, x.PlannedOn, x.DoneOn);
}

/// <summary>PlannedOn — только при создании: день, на который дело записали (может быть офлайн, раньше отправки).</summary>
public sealed record SaveTodoRequest(string Title, DateOnly? PlannedOn = null);

/// <summary>Day — когда отметили «сделано»; передаётся явно, потому что запрос из очереди может уйти позже.</summary>
public sealed record SetTodoDoneRequest(bool Done, DateOnly? Day = null);

/// <summary>Список дел. Null / false — дела нет (или оно чужое).</summary>
public sealed class TodoService(ITodoRepository repository, IUnitOfWork unitOfWork, IUserClock clock)
{
    /// <summary>В порядке добавления (Guid v7 растёт со временем).</summary>
    public async Task<IReadOnlyList<TodoDto>> ListAsync(CancellationToken ct) =>
        [.. (await repository.ListAsync(ct)).OrderBy(x => x.PlannedOn).ThenBy(x => x.Id).Select(TodoDto.From)];

    /// <summary>Создать с id от клиента или переименовать — идемпотентно, для офлайн-очереди.</summary>
    public async Task<TodoDto> UpsertAsync(Guid id, SaveTodoRequest request, CancellationToken ct)
    {
        if (await repository.GetAsync(id, ct) is { } item)
        {
            item.Rename(request.Title);
        }
        else
        {
            item = TodoItem.Create(request.Title, request.PlannedOn ?? await clock.TodayAsync(ct), id);
            repository.Add(item);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return TodoDto.From(item);
    }

    public async Task<TodoDto?> SetDoneAsync(Guid id, SetTodoDoneRequest request, CancellationToken ct)
    {
        if (await repository.GetAsync(id, ct) is not { } item)
            return null;
        item.SetDone(request.Done, request.Day ?? await clock.TodayAsync(ct));
        await unitOfWork.SaveChangesAsync(ct);
        return TodoDto.From(item);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        if (await repository.GetAsync(id, ct) is not { } item)
            return false;
        repository.Remove(item);
        await unitOfWork.SaveChangesAsync(ct);
        return true;
    }
}
