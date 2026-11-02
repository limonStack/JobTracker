using AngleSharp.Dom;
using JobTracker.Application.VacancyChecking;

namespace JobTracker.Infrastructure.VacancyChecking;

/// <summary>
/// jobs.dou.ua. У закрытой вакансии два признака: к заголовку дописано «(вакансія неактивна)»,
/// а блок описания получает класс <c>__inactive</c>. Достаточно любого — так проверка переживёт смену одного из них.
/// </summary>
public sealed class DouChecker(HttpClient http) : HtmlVacancyChecker(http)
{
    private static readonly string[] InactiveMarkers = ["вакансія неактивна", "вакансия неактивна"];

    public override bool CanCheck(Uri url) => url.Host.Equals("jobs.dou.ua", StringComparison.OrdinalIgnoreCase);

    protected override VacancyCheckResult Analyze(IDocument document)
    {
        var title = document.QuerySelector("h1");
        var body = document.QuerySelector(".l-vacancy");
        if (title is null && body is null)
            return VacancyCheckResult.Failed("На странице нет вакансии — изменилась разметка DOU?");

        var inactive = body?.ClassList.Contains("__inactive") == true
                       || InactiveMarkers.Any(m => title?.TextContent.Contains(m, StringComparison.OrdinalIgnoreCase) == true);

        return inactive ? VacancyCheckResult.Inactive() : VacancyCheckResult.Active();
    }
}
