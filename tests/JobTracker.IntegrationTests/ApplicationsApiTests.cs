using System.Net;
using JobTracker.Application.Applications;
using JobTracker.Application.Import;
using JobTracker.Domain.Applications;

namespace JobTracker.IntegrationTests;

public sealed class ApplicationsApiTests : IDisposable
{
    private readonly JobTrackerApiFactory _factory = new();
    private readonly HttpClient _client;

    public ApplicationsApiTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private async Task<JobApplicationDto> CreateAsync(string company = "UNITY-BARS") =>
        await (await _client.PostJsonAsync("/api/applications",
            new SaveApplicationRequest(company, new DateOnly(2026, 9, 21), "jobs.dou.ua/companies/unity-bars/vacancies/373938/", null)))
            .ReadAsync<JobApplicationDto>();

    [Fact]
    public async Task Create_ThenGet_ReturnsSavedApplication()
    {
        var created = await CreateAsync();

        var loaded = await (await _client.GetAsync($"/api/applications/{created.Id}")).ReadAsync<JobApplicationDto>();

        Assert.Equal("UNITY-BARS", loaded.Company);
        Assert.Equal("https://jobs.dou.ua/companies/unity-bars/vacancies/373938/", loaded.Url);
        Assert.Equal(Stage.Applied, loaded.Stage);
    }

    [Fact]
    public async Task Create_WithoutCompany_Returns400ProblemDetails()
    {
        var response = await _client.PostJsonAsync("/api/applications",
            new SaveApplicationRequest("", new DateOnly(2026, 9, 21), null, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Get_Unknown_Returns404()
    {
        var response = await _client.GetAsync($"/api/applications/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ChangeStage_AddsEventAndPersists()
    {
        var created = await CreateAsync();

        await (await _client.PutJsonAsync($"/api/applications/{created.Id}/stage", new ChangeStageRequest(Stage.Hr)))
            .ReadAsync<JobApplicationDto>();
        var loaded = await (await _client.GetAsync($"/api/applications/{created.Id}")).ReadAsync<JobApplicationDto>();

        Assert.Equal(Stage.Hr, loaded.Stage);
        Assert.Equal("hr", Assert.Single(loaded.Events).Text);
    }

    [Fact]
    public async Task AddAndRemoveEvent_Persists()
    {
        var created = await CreateAsync();

        var withEvent = await (await _client.PostJsonAsync($"/api/applications/{created.Id}/events",
            new AddEventRequest(new DateOnly(2026, 9, 29), "разговор с HR"))).ReadAsync<JobApplicationDto>();
        var eventId = Assert.Single(withEvent.Events).Id;

        await (await _client.DeleteAsync($"/api/applications/{created.Id}/events/{eventId}")).ReadAsync<JobApplicationDto>();
        var loaded = await (await _client.GetAsync($"/api/applications/{created.Id}")).ReadAsync<JobApplicationDto>();

        Assert.Empty(loaded.Events);
    }

    [Fact]
    public async Task Delete_RemovesApplication()
    {
        var created = await CreateAsync();

        var response = await _client.DeleteAsync($"/api/applications/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/applications/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task Import_LegacyJson_MapsStagesAndIsIdempotent()
    {
        // формат записи старого трекера из localStorage
        LegacyJob[] legacy =
        [
            new("abc1", "2026-09-22", "Bank Pivdenny", "https://jobs.dou.ua/companies/pivdenny/vacancies/297084/", "rejected", 3,
                [new("2026-09-29", "разговор с HR"), new("2026-10-08", "отказ")], "сделать разбор ошибок"),
            new("abc2", "2026-09-17", "DIIT", "", "applied", 0, [], "военка"),
        ];

        var first = await (await _client.PostJsonAsync("/api/import", legacy)).ReadAsync<ImportResult>();
        var second = await (await _client.PostJsonAsync("/api/import", legacy)).ReadAsync<ImportResult>();
        var all = await (await _client.GetAsync("/api/applications")).ReadAsync<List<JobApplicationDto>>();

        Assert.Equal(2, first.Imported);
        Assert.Equal(0, second.Imported);
        Assert.Equal(2, second.Skipped);
        Assert.Equal(2, all.Count);

        var bank = all.Single(a => a.Company == "Bank Pivdenny");
        Assert.Equal(Stage.Rejected, bank.Stage);
        Assert.Equal(Stage.Tech, bank.ReachedStage);
        Assert.Equal(2, bank.Events.Count);
        Assert.Null(all.Single(a => a.Company == "DIIT").Url);
    }
}
