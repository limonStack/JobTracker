using System.Net;
using JobTracker.Application.Notes;
using JobTracker.Domain.Notes;

namespace JobTracker.IntegrationTests;

public sealed class NotesApiTests : IDisposable
{
    private readonly JobTrackerApiFactory _factory = new();
    private readonly HttpClient _client;

    public NotesApiTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private async Task<IdeaDto> CreateIdeaAsync(string title) =>
        await (await _client.PostJsonAsync("/api/ideas", new CreateIdeaRequest(title))).ReadAsync<IdeaDto>();

    private async Task<List<IdeaDto>> IdeasAsync() =>
        await (await _client.GetAsync("/api/ideas")).ReadAsync<List<IdeaDto>>();

    [Fact]
    public async Task Ideas_CreateUpdateStatus_Delete()
    {
        var idea = await CreateIdeaAsync("Outbox в pet-проекте");
        Assert.Equal(IdeaStatus.New, idea.Status);

        var updated = await (await _client.PutJsonAsync($"/api/ideas/{idea.Id}",
            new UpdateIdeaRequest("Outbox", "через Channel", "JobTracker", "#q12.18"))).ReadAsync<IdeaDto>();
        Assert.Equal(("Outbox", "JobTracker", "12.18"), (updated.Title, updated.Project, updated.Question));

        var done = await (await _client.PutJsonAsync($"/api/ideas/{idea.Id}/status", new ChangeIdeaStatusRequest(IdeaStatus.Done))).ReadAsync<IdeaDto>();
        Assert.NotNull(done.DoneOn);

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/ideas/{idea.Id}")).StatusCode);
        Assert.Empty(await IdeasAsync());
    }

    [Fact]
    public async Task Ideas_DoingFirst_DoneLast()
    {
        var done = await CreateIdeaAsync("применил");
        var doing = await CreateIdeaAsync("пробую");
        await CreateIdeaAsync("новая");
        await _client.PutJsonAsync($"/api/ideas/{done.Id}/status", new ChangeIdeaStatusRequest(IdeaStatus.Done));
        await _client.PutJsonAsync($"/api/ideas/{doing.Id}/status", new ChangeIdeaStatusRequest(IdeaStatus.Doing));

        Assert.Equal(["пробую", "новая", "применил"], (await IdeasAsync()).Select(x => x.Title));
    }

    [Fact]
    public async Task Ideas_ExportMarkdown_GroupedByStatus()
    {
        var idea = await CreateIdeaAsync("Outbox");
        await _client.PutJsonAsync($"/api/ideas/{idea.Id}", new UpdateIdeaRequest("Outbox", "строка 1\nстрока 2", "JobTracker", "12.18"));

        var md = await (await _client.GetAsync("/api/ideas/export.md")).Content.ReadAsStringAsync();

        Assert.Contains("## 💡 идея", md, StringComparison.Ordinal);
        Assert.Contains("- [ ] **Outbox** · проект: JobTracker · вопрос 12.18", md, StringComparison.Ordinal);
        Assert.Contains("  строка 2", md, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ideas_UnknownId_Returns404_EmptyTitle_Returns400()
    {
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.PutJsonAsync($"/api/ideas/{Guid.NewGuid()}", new UpdateIdeaRequest("x", null, null, null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostJsonAsync("/api/ideas", new CreateIdeaRequest(" "))).StatusCode);
    }

    [Fact]
    public async Task Highlights_CreateRecolorDelete()
    {
        var created = await (await _client.PostJsonAsync("/api/highlights",
            new SaveHighlightRequest("07-architecture", "Single Responsibility", "SOLID: ", " — один повод", 120, HighlightColor.Yellow)))
            .ReadAsync<HighlightDto>();

        var recolored = await (await _client.PutJsonAsync($"/api/highlights/{created.Id}",
            new SaveHighlightRequest("07-architecture", created.Text, created.Prefix, created.Suffix, created.Start, HighlightColor.Pink)))
            .ReadAsync<HighlightDto>();
        Assert.Equal(HighlightColor.Pink, recolored.Color);

        var list = await (await _client.GetAsync("/api/highlights")).ReadAsync<List<HighlightDto>>();
        Assert.Equal([recolored], list);

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync($"/api/highlights/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.DeleteAsync($"/api/highlights/{created.Id}")).StatusCode);
    }
}
