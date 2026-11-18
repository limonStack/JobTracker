using System.Net;
using JobTracker.Application.Todos;

namespace JobTracker.IntegrationTests;

public sealed class TodoApiTests : IDisposable
{
    private readonly JobTrackerApiFactory _factory = new();
    private readonly HttpClient _client;

    public TodoApiTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private async Task<List<TodoDto>> TodosAsync() =>
        await (await _client.GetAsync("/api/todos")).ReadAsync<List<TodoDto>>();

    /// <summary>Офлайн-очередь: дело создаётся в браузере со своим id и днём; повтор тех же запросов не создаёт дублей.</summary>
    [Fact]
    public async Task PutWithClientId_CreatesOnce_KeepsOfflineDates()
    {
        var id = Guid.CreateVersion7();
        var create = new SaveTodoRequest("Разобрать 7.6", new DateOnly(2026, 11, 16));
        var done = new SetTodoDoneRequest(true, new DateOnly(2026, 11, 17));

        await _client.PutJsonAsync($"/api/todos/{id}", create);
        await _client.PutJsonAsync($"/api/todos/{id}", create);
        await _client.PutJsonAsync($"/api/todos/{id}/done", done);
        await _client.PutJsonAsync($"/api/todos/{id}/done", done);

        var item = Assert.Single(await TodosAsync());
        Assert.Equal((id, new DateOnly(2026, 11, 16), new DateOnly(2026, 11, 17)), (item.Id, item.PlannedOn, item.DoneOn));
    }

    [Fact]
    public async Task Rename_Undo_Delete()
    {
        var id = Guid.CreateVersion7();
        await _client.PutJsonAsync($"/api/todos/{id}", new SaveTodoRequest("Позвонить"));
        await _client.PutJsonAsync($"/api/todos/{id}/done", new SetTodoDoneRequest(true));

        // переименование не меняет день, на который записано дело
        var renamed = await (await _client.PutJsonAsync($"/api/todos/{id}", new SaveTodoRequest("Позвонить в ACME", new DateOnly(2020, 1, 1))))
            .ReadAsync<TodoDto>();
        Assert.NotEqual(new DateOnly(2020, 1, 1), renamed.PlannedOn);

        var undone = await (await _client.PutJsonAsync($"/api/todos/{id}/done", new SetTodoDoneRequest(false))).ReadAsync<TodoDto>();
        Assert.Equal(("Позвонить в ACME", null), (undone.Title, undone.DoneOn));

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/todos/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync($"/api/todos/{id}")).StatusCode);
        Assert.Empty(await TodosAsync());
    }

    [Fact]
    public async Task DoneOfUnknown_Returns404_EmptyTitle_Returns400()
    {
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.PutJsonAsync($"/api/todos/{Guid.NewGuid()}/done", new SetTodoDoneRequest(true))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await _client.PutJsonAsync($"/api/todos/{Guid.NewGuid()}", new SaveTodoRequest(" "))).StatusCode);
    }

    [Fact]
    public async Task SyncSnapshot_And_Export_ContainTodos()
    {
        await _client.PutJsonAsync($"/api/todos/{Guid.CreateVersion7()}", new SaveTodoRequest("Повторить LINQ"));

        foreach (var url in new[] { "/api/sync", "/api/export" })
        {
            using var json = System.Text.Json.JsonDocument.Parse(await (await _client.GetAsync(url)).Content.ReadAsStringAsync());
            Assert.Equal(1, json.RootElement.GetProperty("todos").GetArrayLength());
        }
    }
}
