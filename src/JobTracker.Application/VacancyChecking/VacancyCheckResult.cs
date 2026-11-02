using JobTracker.Domain.Applications;

namespace JobTracker.Application.VacancyChecking;

/// <summary>
/// Итог проверки (Result pattern): сбой сети или чужая разметка — ожидаемые исходы,
/// поэтому это значение, а не исключение.
/// </summary>
public sealed record VacancyCheckResult(VacancyStatus Status, string? Error = null)
{
    public static VacancyCheckResult Active() => new(VacancyStatus.Active);
    public static VacancyCheckResult Inactive() => new(VacancyStatus.Inactive);
    public static VacancyCheckResult Failed(string error) => new(VacancyStatus.CheckFailed, error);
    public static VacancyCheckResult NotSupported(Uri url) => new(VacancyStatus.Unknown, $"Сайт {url.Host} пока не поддерживается");
}
