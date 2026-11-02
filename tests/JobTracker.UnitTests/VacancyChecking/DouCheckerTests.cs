using System.Net;
using JobTracker.Domain.Applications;
using JobTracker.Infrastructure.VacancyChecking;

namespace JobTracker.UnitTests.VacancyChecking;

public class DouCheckerTests
{
    private static readonly Uri VacancyUrl = new("https://jobs.dou.ua/companies/unity-bars/vacancies/373938/");

    private static DouChecker Checker(StubHttpHandler handler) => new(new HttpClient(handler));

    [Theory]
    [InlineData("https://jobs.dou.ua/companies/unity-bars/vacancies/373938/", true)]
    [InlineData("https://JOBS.DOU.UA/companies/x/vacancies/1/", true)]
    [InlineData("https://djinni.co/jobs/123/", false)]
    [InlineData("https://dou.ua/forums/topic/1/", false)]
    public void CanCheck_OnlyJobsDou(string url, bool expected)
    {
        Assert.Equal(expected, Checker(StubHttpHandler.Html("")).CanCheck(new Uri(url)));
    }

    [Fact]
    public async Task CheckAsync_InactivePage_ReturnsInactive()
    {
        var result = await Checker(StubHttpHandler.Fixture("dou-inactive.html")).CheckAsync(VacancyUrl, default);

        Assert.Equal(VacancyStatus.Inactive, result.Status);
    }

    [Fact]
    public async Task CheckAsync_ActivePage_ReturnsActive()
    {
        var result = await Checker(StubHttpHandler.Fixture("dou-active.html")).CheckAsync(VacancyUrl, default);

        Assert.Equal(VacancyStatus.Active, result.Status);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task CheckAsync_OnlyTitleMarker_ReturnsInactive()
    {
        var handler = StubHttpHandler.Html("<h1 class=\"g-h2\">Middle NET Developer (вакансія неактивна)</h1>");

        var result = await Checker(handler).CheckAsync(VacancyUrl, default);

        Assert.Equal(VacancyStatus.Inactive, result.Status);
    }

    [Fact]
    public async Task CheckAsync_OnlyInactiveClass_ReturnsInactive()
    {
        var handler = StubHttpHandler.Html("<div class=\"l-vacancy __inactive\"><h1>Middle NET Developer</h1></div>");

        var result = await Checker(handler).CheckAsync(VacancyUrl, default);

        Assert.Equal(VacancyStatus.Inactive, result.Status);
    }

    [Fact]
    public async Task CheckAsync_PageWithoutVacancy_ReturnsFailed()
    {
        var result = await Checker(StubHttpHandler.Html("<html><body><p>Just a moment...</p></body></html>"))
            .CheckAsync(VacancyUrl, default);

        Assert.Equal(VacancyStatus.CheckFailed, result.Status);
        Assert.Contains("разметка", result.Error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task CheckAsync_ErrorStatus_ReturnsFailedWithCode(HttpStatusCode status)
    {
        var result = await Checker(StubHttpHandler.Html("", status)).CheckAsync(VacancyUrl, default);

        Assert.Equal(VacancyStatus.CheckFailed, result.Status);
        Assert.Contains(((int)status).ToString(System.Globalization.CultureInfo.InvariantCulture), result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckAsync_NetworkError_ReturnsFailed()
    {
        var result = await Checker(StubHttpHandler.Throws(new HttpRequestException("No such host")))
            .CheckAsync(VacancyUrl, default);

        Assert.Equal(VacancyStatus.CheckFailed, result.Status);
    }

    [Fact]
    public async Task CheckAsync_Timeout_ReturnsFailed()
    {
        var result = await Checker(StubHttpHandler.Throws(new TaskCanceledException("timeout")))
            .CheckAsync(VacancyUrl, default);

        Assert.Equal(VacancyStatus.CheckFailed, result.Status);
    }

    [Fact]
    public async Task CheckAsync_CallerCancelled_Throws()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Checker(StubHttpHandler.Throws(new TaskCanceledException())).CheckAsync(VacancyUrl, cts.Token));
    }
}
