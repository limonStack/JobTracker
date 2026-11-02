using JobTracker.Application.Abstractions;
using JobTracker.Domain.Applications;
using JobTracker.Domain.Common;

namespace JobTracker.Application.Import;

/// <summary>Запись старого трекера из localStorage: { id, d, c, u, s, m, e: [{ d, t }], n }.</summary>
public sealed record LegacyJob(string Id, string D, string C, string? U, string? S, int M, IReadOnlyList<LegacyEvent>? E, string? N);

public sealed record LegacyEvent(string D, string T);

public sealed record ImportResult(int Imported, int Skipped, IReadOnlyList<string> Errors);

/// <summary>
/// Переносит записи из старого трекера. Идемпотентен: запись с уже известным LegacyId пропускается,
/// поэтому импорт можно запускать повторно. Битая запись не валит весь импорт — попадает в Errors.
/// </summary>
public sealed class LegacyImportService(IJobApplicationRepository repository, IUnitOfWork unitOfWork)
{
    // порядок воронки в старом трекере: m — индекс в этом массиве
    private static readonly Stage[] Funnel = [Stage.Applied, Stage.Replied, Stage.Hr, Stage.Tech, Stage.Offer];

    private static readonly Dictionary<string, Stage> Stages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["applied"] = Stage.Applied,
        ["replied"] = Stage.Replied,
        ["hr"] = Stage.Hr,
        ["tech"] = Stage.Tech,
        ["offer"] = Stage.Offer,
        ["rejected"] = Stage.Rejected,
        ["silent"] = Stage.Silent,
    };

    public async Task<ImportResult> ImportAsync(IReadOnlyList<LegacyJob> jobs, CancellationToken ct)
    {
        var known = await repository.GetLegacyIdsAsync(ct);
        int imported = 0, skipped = 0;
        var errors = new List<string>();

        foreach (var job in jobs)
        {
            if (!known.Add(job.Id))
            {
                skipped++;
                continue;
            }

            try
            {
                repository.Add(Map(job));
                imported++;
            }
            catch (Exception ex) when (ex is DomainException or FormatException)
            {
                errors.Add($"{job.C}: {ex.Message}");
            }
        }

        await unitOfWork.SaveChangesAsync(ct);
        return new ImportResult(imported, skipped, errors);
    }

    private static JobApplication Map(LegacyJob job)
    {
        var application = JobApplication.Create(job.C, DateOnly.Parse(job.D, System.Globalization.CultureInfo.InvariantCulture),
            job.U, job.N, legacyId: job.Id);

        var stage = Stages.GetValueOrDefault(job.S ?? "", Stage.Applied);
        application.RestoreStage(stage, Funnel[Math.Clamp(job.M, 0, Funnel.Length - 1)]);

        foreach (var e in job.E ?? [])
            application.AddEvent(DateOnly.Parse(e.D, System.Globalization.CultureInfo.InvariantCulture), e.T);

        return application;
    }
}
