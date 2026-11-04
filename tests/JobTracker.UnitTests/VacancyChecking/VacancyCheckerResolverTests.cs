using JobTracker.Application.Abstractions;
using JobTracker.Application.VacancyChecking;
using JobTracker.Domain.Applications;
using JobTracker.Infrastructure.VacancyChecking;

namespace JobTracker.UnitTests.VacancyChecking;

public class VacancyCheckerResolverTests
{
    private static readonly DouChecker Dou = new(new HttpClient(StubHttpHandler.Fixture("dou-inactive.html")));

    [Fact]
    public void Resolve_DouUrl_ReturnsDouChecker()
    {
        var resolver = new VacancyCheckerResolver([Dou]);

        Assert.Same(Dou, resolver.Resolve(new Uri("https://jobs.dou.ua/companies/x/vacancies/1/")));
    }

    [Fact]
    public void Resolve_SeveralCheckers_PicksBySite()
    {
        var workUa = new WorkUaChecker(new HttpClient(StubHttpHandler.Html("")));
        var resolver = new VacancyCheckerResolver([Dou, workUa]);

        Assert.Same(workUa, resolver.Resolve(new Uri("https://www.work.ua/jobs/7062997/")));
        Assert.Same(Dou, resolver.Resolve(new Uri("https://jobs.dou.ua/companies/x/vacancies/1/")));
    }

    [Fact]
    public async Task CheckAsync_UnsupportedSite_ReturnsUnknownWithoutRequest()
    {
        var resolver = new VacancyCheckerResolver(Array.Empty<IVacancyChecker>());

        var result = await resolver.CheckAsync(new Uri("https://djinni.co/jobs/1/"), default);

        Assert.Equal(VacancyStatus.Unknown, result.Status);
        Assert.Contains("djinni.co", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckAsync_DouUrl_DelegatesToChecker()
    {
        var resolver = new VacancyCheckerResolver([Dou]);

        var result = await resolver.CheckAsync(new Uri("https://jobs.dou.ua/companies/x/vacancies/1/"), default);

        Assert.Equal(VacancyStatus.Inactive, result.Status);
    }
}
