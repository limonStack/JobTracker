using JobTracker.Application.Todos;

namespace JobTracker.Api.Endpoints;

internal static class TodoEndpoints
{
    public static IEndpointRouteBuilder MapTodoEndpoints(this IEndpointRouteBuilder app)
    {
        var todos = app.MapGroup("/api/todos").WithTags("Todos");

        todos.MapGet("/", (TodoService service, CancellationToken ct) => service.ListAsync(ct));

        // PUT с id от клиента — создать или переименовать (офлайн-очередь); POST нет: id всегда даёт браузер
        todos.MapPut("/{id:guid}", async (Guid id, SaveTodoRequest request, TodoService service, CancellationToken ct) =>
            Results.Ok(await service.UpsertAsync(id, request, ct)));

        todos.MapPut("/{id:guid}/done", async (Guid id, SetTodoDoneRequest request, TodoService service, CancellationToken ct) =>
            await service.SetDoneAsync(id, request, ct) is { } dto ? Results.Ok(dto) : Results.NotFound());

        todos.MapDelete("/{id:guid}", async (Guid id, TodoService service, CancellationToken ct) =>
            await service.DeleteAsync(id, ct) ? Results.NoContent() : Results.NotFound());

        return app;
    }
}
