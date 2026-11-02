using JobTracker.Domain.Applications;
using JobTracker.Domain.Common;

namespace JobTracker.UnitTests.Domain;

public class VacancyUrlTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_Empty_ReturnsNull(string? raw)
    {
        Assert.Null(VacancyUrl.Create(raw));
    }

    [Fact]
    public void Create_WithoutScheme_AddsHttps()
    {
        var url = VacancyUrl.Create("jobs.dou.ua/companies/unity-bars/vacancies/373938/");

        Assert.Equal("https://jobs.dou.ua/companies/unity-bars/vacancies/373938/", url!.Value);
    }

    [Fact]
    public void Create_NonHttpScheme_Throws()
    {
        Assert.Throws<DomainException>(() => VacancyUrl.Create("ftp://example.com/file"));
    }

    [Fact]
    public void SameValue_AreEqual()
    {
        Assert.Equal(VacancyUrl.Create("https://dou.ua/x"), VacancyUrl.Create("dou.ua/x"));
    }
}
