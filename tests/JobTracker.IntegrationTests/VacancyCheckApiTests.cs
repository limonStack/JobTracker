using System.Net;
using JobTracker.Application.Applications;
using JobTracker.Application.VacancyChecking;
using JobTracker.Domain.Applications;

namespace JobTracker.IntegrationTests;

public sealed class VacancyCheckApiTests : IDisposable
{
    private const string ClosedUrl = "https://jobs.dou.ua/companies/unity-bars/vacancies/373938/";
    private const string OpenUrl = "https://jobs.dou.ua/companies/freitty/vacancies/366693/";

    private readonly JobTrackerApiFactory _factory = new();
    private readonly HttpClient _client;

    public VacancyCheckApiTests()
    {
        _client = _factory.CreateClient();
        _factory.Checker.MarkInactive(ClosedUrl);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private async Task<JobApplicationDto> CreateAsync(string company, string? url) =>
        await (await _client.PostJsonAsync("/api/applications",
            new SaveApplicationRequest(company, new DateOnly(2026, 10, 1), url, null))).ReadAsync<JobApplicationDto>();

    private async Task<JobApplicationDto> GetAsync(Guid id) =>
        await (await _client.GetAsync($"/api/applications/{id}")).ReadAsync<JobApplicationDto>();

    [Fact]
    public async Task CheckOne_ClosedVacancy_MarksInactive()
    {
        var app = await CreateAsync("UNITY-BARS", ClosedUrl);

        var checkedApp = await (await _client.PostAsync($"/api/applications/{app.Id}/check", null)).ReadAsync<JobApplicationDto>();

        Assert.Equal(VacancyStatus.Inactive, checkedApp.VacancyStatus);
        Assert.NotNull(checkedApp.ClosedAt);
        Assert.Equal(VacancyStatus.Inactive, (await GetAsync(app.Id)).VacancyStatus);
    }

    [Fact]
    public async Task CheckOne_Unknown_Returns404()
    {
        var response = await _client.PostAsync($"/api/applications/{Guid.NewGuid()}/check", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CheckAll_RunsInBackground_AndSkipsWhatShouldNotBeChecked()
    {
        var closed = await CreateAsync("UNITY-BARS", ClosedUrl);
        var open = await CreateAsync("FREITTY", OpenUrl);
        var otherSite = await CreateAsync("Djinni company", "https://djinni.co/jobs/1/");
        var noUrl = await CreateAsync("DIIT", null);
        var rejected = await CreateAsync("Bank", "https://jobs.dou.ua/companies/pivdenny/vacancies/297084/");
        await _client.PutJsonAsync($"/api/applications/{rejected.Id}/stage", new ChangeStageRequest(Stage.Rejected));

        var accepted = await _client.PostAsync("/api/checks", null);
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        var status = await WaitUntilFinishedAsync();

        Assert.Equal(new CheckAllResult(Checked: 2, Closed: 1, Failed: 0), status.LastResult);
        Assert.Equal((2, 2), (status.Total, status.Done));
        Assert.Null(status.Current);
        Assert.Equal(
            [(closed.Id, VacancyStatus.Inactive), (open.Id, VacancyStatus.Active)],
            status.Log.Select(e => (e.ApplicationId, e.Status)).OrderBy(e => e.Status == VacancyStatus.Active));
        Assert.Equal(VacancyStatus.Inactive, (await GetAsync(closed.Id)).VacancyStatus);
        Assert.Equal(VacancyStatus.Active, (await GetAsync(open.Id)).VacancyStatus);
        Assert.Equal(VacancyStatus.Unknown, (await GetAsync(otherSite.Id)).VacancyStatus);
        Assert.Equal(VacancyStatus.Unknown, (await GetAsync(noUrl.Id)).VacancyStatus);
        Assert.Equal(VacancyStatus.Unknown, (await GetAsync(rejected.Id)).VacancyStatus);
    }

    private async Task<VacancyCheckStatus> WaitUntilFinishedAsync()
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            var status = await (await _client.GetAsync("/api/checks")).ReadAsync<VacancyCheckStatus>();
            if (!status.Running && status.LastFinishedAt is not null)
                return status;
            await Task.Delay(100);
        }

        throw new TimeoutException("Фоновая проверка не завершилась за 15 секунд");
    }
}
