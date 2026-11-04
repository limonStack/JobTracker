using JobTracker.Application.Abstractions;
using JobTracker.Application.Applications;
using JobTracker.Domain.Applications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobTracker.Application.VacancyChecking;

public sealed record CheckAllResult(int Checked, int Closed, int Failed);

/// <summary>Проверяет вакансии на сайтах и записывает результат в отклики.</summary>
public sealed partial class VacancyCheckService(
    IJobApplicationRepository repository,
    IUnitOfWork unitOfWork,
    VacancyCheckerResolver resolver,
    VacancyCheckProgress progress,
    IOptions<VacancyCheckOptions> options,
    TimeProvider time,
    ILogger<VacancyCheckService> logger)
{
    /// <summary>
    /// Ручная проверка одного отклика — выполняется сразу, даже если отклик уже закрыт.
    /// Null — отклик не найден.
    /// </summary>
    public async Task<JobApplicationDto?> CheckOneAsync(Guid id, CancellationToken ct)
    {
        if (await repository.GetAsync(id, ct) is not { } application)
            return null;

        if (application.Url is { } url)
        {
            var result = await resolver.CheckAsync(url.ToUri(), ct);
            application.RecordCheck(result.Status, time.GetUtcNow(), result.Error);
            await unitOfWork.SaveChangesAsync(ct);
        }

        return JobApplicationDto.From(application);
    }

    /// <summary>
    /// Проверяет все отклики, которые имеет смысл проверять. Сохраняет после каждой вакансии:
    /// если обход прервётся, уже полученные результаты не пропадут.
    /// </summary>
    public async Task<CheckAllResult> CheckAllAsync(CancellationToken ct)
    {
        var candidates = (await repository.ListForVacancyCheckAsync(ct))
            .Where(a => resolver.Resolve(a.Url!.ToUri()) is not null)   // сайты без проверяльщика не дёргаем
            .ToList();
        int closed = 0, failed = 0;
        progress.Planned(candidates.Count);

        for (var i = 0; i < candidates.Count; i++)
        {
            if (i > 0)
                await Task.Delay(options.Value.DelayBetweenRequests, time, ct);

            var application = candidates[i];
            progress.Checking(application.Company);
            var result = await resolver.CheckAsync(application.Url!.ToUri(), ct);
            var wasInactive = application.VacancyStatus == VacancyStatus.Inactive;
            var now = time.GetUtcNow();
            application.RecordCheck(result.Status, now, result.Error);
            await unitOfWork.SaveChangesAsync(ct);
            progress.Checked(new VacancyCheckLogEntry(now, application.Id, application.Company, result.Status, result.Error));

            if (result.Status == VacancyStatus.CheckFailed)
            {
                failed++;
                LogCheckFailed(application.Company, result.Error);
            }
            else
            {
                LogChecked(application.Company, result.Status);
                if (!wasInactive && application.VacancyStatus == VacancyStatus.Inactive)
                    closed++;
            }
        }

        LogCheckAllDone(candidates.Count, closed, failed);
        return new CheckAllResult(candidates.Count, closed, failed);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Не удалось проверить вакансию {Company}: {Error}")]
    private partial void LogCheckFailed(string company, string? error);

    [LoggerMessage(Level = LogLevel.Information, Message = "Вакансия {Company}: {Status}")]
    private partial void LogChecked(string company, VacancyStatus status);

    [LoggerMessage(Level = LogLevel.Information, Message = "Проверка вакансий: проверено {Checked}, закрылось {Closed}, ошибок {Failed}")]
    private partial void LogCheckAllDone(int @checked, int closed, int failed);
}
