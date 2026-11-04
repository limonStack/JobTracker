using System.Globalization;
using System.Text.Json;
using JobTracker.Application.Applications;
using JobTracker.Application.Import;
using JobTracker.Application.Notes;
using JobTracker.Application.Study;
using JobTracker.Domain.Notes;
using JobTracker.Domain.Study;

namespace JobTracker.IntegrationTests;

/// <summary>Перенос localStorage старого сайта: форматы как в 300questions/ANSWERS.html.</summary>
public sealed class LegacySiteImportTests : IDisposable
{
    private readonly JobTrackerApiFactory _factory = new() { ImportContent = true };
    private readonly HttpClient _client;

    public LegacySiteImportTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private static readonly string[] DoneCodes = ["1.1", "7.1", "7.2", "99.9"];   // 99.9 — такого вопроса нет

    private static string Day(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Выгрузка как из консоли старого сайта: значения — строки JSON, как они лежат в localStorage.</summary>
    private static Dictionary<string, string?> Storage(DateOnly today)
    {
        string J(object o) => JsonSerializer.Serialize(o);
        var yesterday = Day(today.AddDays(-1));
        return new()
        {
            ["net-interview-progress-v1"] = J(new
            {
                done = DoneCodes,
                doneAt = new Dictionary<string, string> { ["1.1"] = yesterday, ["7.1"] = Day(today) },   // у 7.2 даты нет
                updated = "2026-11-01T10:00:00Z",
            }),
            ["net-interview-workouts-v1"] = J(new { days = new Dictionary<string, bool> { [yesterday] = true, [Day(today.AddDays(5))] = true } }),
            ["net-interview-ai-v1"] = J(new { days = new Dictionary<string, bool> { [Day(today)] = true } }),
            ["net-interview-review-v1"] = J(new Dictionary<string, string[]>
            {
                [yesterday] = ["1.1"],
                [Day(today.AddDays(-45))] = ["7.1"],                    // старше 30 дней — не переносится
            }),
            ["net-interview-ideas-v1"] = J(new[]
            {
                new { id = "i1", d = "2026-10-20", t = "Outbox", n = "через Channel", p = "JobTracker", q = "12.18", s = "done", done = "2026-10-25" },
                new { id = "i2", d = "2026-10-21", t = "", n = "", p = "", q = "", s = "new", done = "" },   // пустая — ошибка
            }),
            ["net-interview-highlights-v1"] = J(new[]
            {
                new { id = "h1", sec = "07-architecture", text = "Single Responsibility", prefix = "SOLID: ", suffix = " — один", start = 120, end = 141, color = "green" },
            }),
            ["net-interview-jobs-v1"] = J(new[]
            {
                new { id = "j1", d = "2026-09-21", c = "UNITY-BARS", u = "https://jobs.dou.ua/companies/unity-bars/vacancies/373938/", s = "tech", m = 3,
                      e = new[] { new { d = "2026-09-29", t = "разговор с HR" } }, n = "" },
            }),
            ["net-interview-calendar-open"] = "1",
            ["net-interview-focus"] = "0",
            ["something-else"] = "x",
        };
    }

    [Fact]
    public async Task Import_AllModules_ThenAgain_NoDuplicates()
    {
        var today = (await (await _client.GetAsync("/api/study")).ReadAsync<StudyStateDto>()).Today;
        var storage = Storage(today);

        var first = await (await _client.PostJsonAsync("/api/import/site", storage)).ReadAsync<LegacySiteImportResult>();

        Assert.Equal(3, first.Progress!.Imported);
        Assert.Single(first.Progress.Errors);                    // 99.9
        Assert.Equal(1, first.Workouts!.Imported);
        Assert.Single(first.Workouts.Errors);                    // будущий день
        Assert.Equal(1, first.Ai!.Imported);
        Assert.Equal((1, 1), (first.Reviews!.Imported, first.Reviews.Skipped));
        Assert.Equal(1, first.Ideas!.Imported);
        Assert.Single(first.Ideas.Errors);
        Assert.Equal(1, first.Highlights!.Imported);
        Assert.Equal(1, first.Jobs!.Imported);
        Assert.True(first.SettingsApplied);
        Assert.Equal(["something-else"], first.UnknownKeys);

        var second = await (await _client.PostJsonAsync("/api/import/site", storage)).ReadAsync<LegacySiteImportResult>();
        Assert.Equal(0, second.Progress!.Imported + second.Workouts!.Imported + second.Ai!.Imported + second.Reviews!.Imported
                         + second.Ideas!.Imported + second.Highlights!.Imported + second.Jobs!.Imported);

        var state = await (await _client.GetAsync("/api/study")).ReadAsync<StudyStateDto>();
        Assert.Equal(
            [new StudyMarkDto("1.1", today.AddDays(-1)), new StudyMarkDto("7.1", today), new StudyMarkDto("7.2", null)],
            state.Marks);
        Assert.Equal(2, state.Activities.Count);
        Assert.Contains(state.Activities, a => a.Kind == ActivityKind.Ai && a.Day == today);
        Assert.Equal([new ReviewDto(today.AddDays(-1), "1.1")], state.Reviews);
        Assert.True(state.Settings.CalendarOpen);

        var idea = Assert.Single(await (await _client.GetAsync("/api/ideas")).ReadAsync<List<IdeaDto>>());
        Assert.Equal((IdeaStatus.Done, new DateOnly(2026, 10, 25), "12.18"), (idea.Status, idea.DoneOn!.Value, idea.Question));

        var highlight = Assert.Single(await (await _client.GetAsync("/api/highlights")).ReadAsync<List<HighlightDto>>());
        Assert.Equal(("07-architecture", HighlightColor.Green, 120), (highlight.Section, highlight.Color, highlight.Start));

        var job = Assert.Single(await (await _client.GetAsync("/api/applications")).ReadAsync<List<JobApplicationDto>>());
        Assert.Equal("UNITY-BARS", job.Company);
    }

    [Fact]
    public async Task Import_BrokenJson_ReportsErrorForThatModuleOnly()
    {
        var storage = new Dictionary<string, string?>
        {
            ["net-interview-ideas-v1"] = "{not json",
            ["net-interview-ai-v1"] = """{"days":{"2026-10-01":true}}""",
        };

        var result = await (await _client.PostJsonAsync("/api/import/site", storage)).ReadAsync<LegacySiteImportResult>();

        Assert.Single(result.Ideas!.Errors);
        Assert.Equal(1, result.Ai!.Imported);
        Assert.Null(result.Progress);
    }
}
