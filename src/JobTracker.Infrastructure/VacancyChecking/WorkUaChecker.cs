using System.Net;
using AngleSharp.Dom;
using JobTracker.Application.VacancyChecking;

namespace JobTracker.Infrastructure.VacancyChecking;

/// <summary>
/// www.work.ua. Закрытую вакансию сайт не помечает, а убирает: недавно снятая отдаёт 301 на поиск
/// с <c>?job_removed=1</c>, давняя — 410 со страницей «вакансію не знайдено». Открытая — страница с <c>h1#h1-name</c>.
/// </summary>
public sealed class WorkUaChecker(HttpClient http) : HtmlVacancyChecker(http)
{
    private static readonly string[] Hosts = ["work.ua", "www.work.ua"];
    private static readonly string[] RemovedMarkers = ["вакансію не знайдено", "вакансия не найдена"];

    public override bool CanCheck(Uri url) => Hosts.Contains(url.Host, StringComparer.OrdinalIgnoreCase);

    protected override VacancyCheckResult? AnalyzeResponse(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Gone)
            return VacancyCheckResult.Inactive();

        // HttpClient прошёл по редиректу сам — в RequestMessage уже конечный адрес
        var finalUrl = response.RequestMessage?.RequestUri;
        return finalUrl?.Query.Contains("job_removed=1", StringComparison.OrdinalIgnoreCase) == true
            ? VacancyCheckResult.Inactive()
            : null;
    }

    protected override VacancyCheckResult Analyze(IDocument document)
    {
        if (document.QuerySelector("h1#h1-name") is not null)
            return VacancyCheckResult.Active();

        var title = document.QuerySelector("h1")?.TextContent;
        if (RemovedMarkers.Any(m => title?.Contains(m, StringComparison.OrdinalIgnoreCase) == true))
            return VacancyCheckResult.Inactive();

        return VacancyCheckResult.Failed("На странице нет вакансии — изменилась разметка Work.ua?");
    }
}
