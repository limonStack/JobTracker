namespace JobTracker.Application.Abstractions;

/// <summary>
/// Проверка вакансии на конкретном сайте (Strategy): на каждый сайт — своя реализация,
/// подходящую выбирает <see cref="VacancyChecking.VacancyCheckerResolver"/> по адресу.
/// </summary>
public interface IVacancyChecker
{
    bool CanCheck(Uri url);

    /// <summary>Не бросает исключений на ожидаемые сбои (сеть, 404, разметка) — возвращает Failed.</summary>
    Task<VacancyChecking.VacancyCheckResult> CheckAsync(Uri url, CancellationToken ct);
}
