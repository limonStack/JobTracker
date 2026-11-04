using System.Globalization;
using System.Net;
using JobTracker.Application.Study;
using JobTracker.Domain.Study;

namespace JobTracker.IntegrationTests;

/// <summary>Прогресс, треки календаря, повторение и настройки — через API, на импортированных текстах ответов.</summary>
public sealed class StudyApiTests : IDisposable
{
    private readonly JobTrackerApiFactory _factory = new() { ImportContent = true };
    private readonly HttpClient _client;

    public StudyApiTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private Task<StudyStateDto> StateAsync() => _client.GetAsync("/api/study").ContinueWith(t => t.Result.ReadAsync<StudyStateDto>()).Unwrap();

    private static string Day(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    [Fact]
    public async Task Mark_Today_ThenBackfill_ThenUnmark()
    {
        var today = (await StateAsync()).Today;

        var marked = await (await _client.PutJsonAsync("/api/study/marks/7.1", new MarkStudiedRequest(null))).ReadAsync<StudyMarkDto>();
        Assert.Equal(new StudyMarkDto("7.1", today), marked);

        // та же тема — за вчера: отметка переносится, а не дублируется
        await _client.PutJsonAsync("/api/study/marks/7.1", new MarkStudiedRequest(today.AddDays(-1)));
        Assert.Equal([new StudyMarkDto("7.1", today.AddDays(-1))], (await StateAsync()).Marks);

        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync("/api/study/marks/7.1")).StatusCode);
        Assert.Empty((await StateAsync()).Marks);
    }

    [Fact]
    public async Task Marks_SortedByQuestionNumber()
    {
        foreach (var code in new[] { "7.10", "7.2", "1.1" })
            await _client.PutJsonAsync($"/api/study/marks/{code}", new MarkStudiedRequest(null));

        Assert.Equal(["1.1", "7.2", "7.10"], (await StateAsync()).Marks.Select(m => m.Code));
    }

    [Fact]
    public async Task Mark_FutureDay_Returns400()
    {
        var today = (await StateAsync()).Today;

        var response = await _client.PutJsonAsync("/api/study/marks/7.1", new MarkStudiedRequest(today.AddDays(2)));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Mark_UnknownQuestion_Returns404_BadCode_Returns400()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PutJsonAsync("/api/study/marks/99.1", new MarkStudiedRequest(null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PutJsonAsync("/api/study/marks/abc", new MarkStudiedRequest(null))).StatusCode);
    }

    [Fact]
    public async Task Activities_PutTwice_StoredOnce_DeleteRemoves()
    {
        var day = Day((await StateAsync()).Today);

        await _client.PutAsync($"/api/study/activities/workout/{day}", null);
        await _client.PutAsync($"/api/study/activities/workout/{day}", null);
        await _client.PutAsync($"/api/study/activities/AI/{day}", null);
        var state = await StateAsync();
        Assert.Equal(2, state.Activities.Count);
        Assert.Contains(state.Activities, a => a.Kind == ActivityKind.Ai);

        await _client.DeleteAsync($"/api/study/activities/workout/{day}");
        Assert.Equal([ActivityKind.Ai], (await StateAsync()).Activities.Select(a => a.Kind));
    }

    [Fact]
    public async Task Activity_UnknownKind_Returns404()
    {
        var day = Day((await StateAsync()).Today);

        Assert.Equal(HttpStatusCode.NotFound, (await _client.PutAsync($"/api/study/activities/swim/{day}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PutAsync($"/api/study/activities/0/{day}", null)).StatusCode);
    }

    [Fact]
    public async Task Reviews_OlderThan30Days_RemovedOnNextChange()
    {
        var today = (await StateAsync()).Today;
        await _client.PutAsync($"/api/study/reviews/{Day(today.AddDays(-40))}/7.1", null);   // уже старая
        Assert.Single((await StateAsync()).Reviews);

        await _client.PutAsync($"/api/study/reviews/{Day(today.AddDays(-1))}/7.2", null);

        Assert.Equal([new ReviewDto(today.AddDays(-1), "7.2")], (await StateAsync()).Reviews);
    }

    [Fact]
    public async Task Settings_DefaultsThenUpdate()
    {
        var defaults = await (await _client.GetAsync("/api/settings")).ReadAsync<SettingsDto>();
        Assert.Equal(new SettingsDto(10, 3, null, false, null), defaults);

        var updated = await (await _client.PutJsonAsync("/api/settings", new SettingsDto(12, 4, true, true, "Europe/Kyiv"))).ReadAsync<SettingsDto>();

        Assert.Equal(updated, (await StateAsync()).Settings);
        Assert.Equal((12, true, "Europe/Kyiv"), (updated.DailyTheoryGoal, updated.FocusMode, updated.TimeZone));
    }

    [Fact]
    public async Task Settings_Invalid_Returns400()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PutJsonAsync("/api/settings", new SettingsDto(0, 3, null, false, null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PutJsonAsync("/api/settings", new SettingsDto(10, 3, null, false, "Mars/Olympus"))).StatusCode);
    }
}
