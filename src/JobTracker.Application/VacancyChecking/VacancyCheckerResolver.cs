using JobTracker.Application.Abstractions;

namespace JobTracker.Application.VacancyChecking;

/// <summary>
/// Выбирает проверяльщика по адресу вакансии из всех зарегистрированных в DI.
/// Новый сайт = новый класс IVacancyChecker + регистрация; этот код не меняется (Open/Closed).
/// </summary>
public sealed class VacancyCheckerResolver(IEnumerable<IVacancyChecker> checkers)
{
    public IVacancyChecker? Resolve(Uri url) => checkers.FirstOrDefault(c => c.CanCheck(url));

    public Task<VacancyCheckResult> CheckAsync(Uri url, CancellationToken ct) =>
        Resolve(url)?.CheckAsync(url, ct) ?? Task.FromResult(VacancyCheckResult.NotSupported(url));
}
