using System.Net;
using JobTracker.Domain.Applications;
using JobTracker.Infrastructure.VacancyChecking;

namespace JobTracker.UnitTests.VacancyChecking;

public class WorkUaCheckerTests
{
    private static readonly Uri VacancyUrl = new("https://www.work.ua/jobs/7062997/");

    private static WorkUaChecker Checker(StubHttpHandler handler) => new(new HttpClient(handler));

    [Theory]
    [InlineData("https://www.work.ua/jobs/7062997/", true)]
    [InlineData("https://work.ua/jobs/7062997/", true)]
    [InlineData("https://WWW.WORK.UA/jobs/1/", true)]
    [InlineData("https://jobs.dou.ua/companies/x/vacancies/1/", false)]
    [InlineData("https://work.ua.example.com/jobs/1/", false)]
    public void CanCheck_OnlyWorkUa(string url, bool expected)
    {
        Assert.Equal(expected, Checker(StubHttpHandler.Html("")).CanCheck(new Uri(url)));
    }

    [Fact]
    public async Task CheckAsync_ActivePage_ReturnsActive()
    {
        var result = await Checker(StubHttpHandler.Fixture("workua-active.html")).CheckAsync(VacancyUrl, default);

        Assert.Equal(VacancyStatus.Active, result.Status);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task CheckAsync_Gone_ReturnsInactive()
    {
        var handler = StubHttpHandler.Fixture("workua-removed.html", HttpStatusCode.Gone);

        var result = await Checker(handler).CheckAsync(VacancyUrl, default);

        Assert.Equal(VacancyStatus.Inactive, result.Status);
    }

    [Fact]
    public async Task CheckAsync_RemovedPageWith200_ReturnsInactive()
    {
        var result = await Checker(StubHttpHandler.Fixture("workua-removed.html")).CheckAsync(VacancyUrl, default);

        Assert.Equal(VacancyStatus.Inactive, result.Status);
    }

    [Fact]
    public async Task CheckAsync_RedirectedToSearchWithJobRemoved_ReturnsInactive()
    {
        var search = new Uri("https://www.work.ua/jobs-kyiv-%D0%B1%D0%B0%D1%80%D0%B8%D1%81%D1%82%D0%B0/?job_removed=1");

        var result = await Checker(StubHttpHandler.RedirectedTo(search, "<h1>Вакансії баристи у Києві</h1>"))
            .CheckAsync(VacancyUrl, default);

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
    public async Task CheckAsync_ErrorStatus_ReturnsFailed(HttpStatusCode status)
    {
        var result = await Checker(StubHttpHandler.Html("", status)).CheckAsync(VacancyUrl, default);

        Assert.Equal(VacancyStatus.CheckFailed, result.Status);
    }
}
