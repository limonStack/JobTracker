using System.Net;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using JobTracker.Application.Abstractions;
using JobTracker.Application.VacancyChecking;

namespace JobTracker.Infrastructure.VacancyChecking;

/// <summary>
/// Общий скелет проверки по HTML-странице (Template Method): загрузить → разобрать → решить.
/// Наследник реализует только <see cref="CanCheck"/> и <see cref="Analyze"/> — признаки своего сайта.
/// </summary>
public abstract class HtmlVacancyChecker(HttpClient http) : IVacancyChecker
{
    private static readonly HtmlParser Parser = new();

    public abstract bool CanCheck(Uri url);

    protected abstract VacancyCheckResult Analyze(IDocument document);

    /// <summary>
    /// Решение по самому ответу, до разбора HTML: некоторые сайты сообщают о закрытии кодом (410)
    /// или редиректом. <c>null</c> — ответ ничего не говорит, дальше обычная обработка.
    /// </summary>
    protected virtual VacancyCheckResult? AnalyzeResponse(HttpResponseMessage response) => null;

    public async Task<VacancyCheckResult> CheckAsync(Uri url, CancellationToken ct)
    {
        try
        {
            using var response = await http.GetAsync(url, ct);
            if (AnalyzeResponse(response) is { } decided)
                return decided;
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
                return VacancyCheckResult.Failed($"Страница не найдена ({(int)response.StatusCode})");
            if (!response.IsSuccessStatusCode)
                return VacancyCheckResult.Failed($"Сайт ответил {(int)response.StatusCode}");

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await Parser.ParseDocumentAsync(stream, ct);
            return Analyze(document);
        }
        catch (HttpRequestException ex)
        {
            return VacancyCheckResult.Failed($"Ошибка сети: {ex.Message}");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return VacancyCheckResult.Failed("Сайт не ответил вовремя");
        }
    }
}
