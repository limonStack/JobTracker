using System.Collections.Concurrent;
using JobTracker.Application.Abstractions;
using JobTracker.Application.VacancyChecking;

namespace JobTracker.IntegrationTests;

/// <summary>Проверяльщик для jobs.dou.ua без сети: закрытые вакансии задаёт тест, остальные — активны.</summary>
public sealed class FakeVacancyChecker : IVacancyChecker
{
    private readonly ConcurrentDictionary<string, bool> _inactive = new();

    public ConcurrentBag<Uri> Checked { get; } = [];

    public void MarkInactive(string url) => _inactive[url] = true;

    public bool CanCheck(Uri url) => url.Host == "jobs.dou.ua";

    public Task<VacancyCheckResult> CheckAsync(Uri url, CancellationToken ct)
    {
        Checked.Add(url);
        return Task.FromResult(_inactive.ContainsKey(url.ToString()) ? VacancyCheckResult.Inactive() : VacancyCheckResult.Active());
    }
}
