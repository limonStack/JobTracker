using JobTracker.Application.Abstractions;
using JobTracker.Domain.Applications;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.Infrastructure.Persistence;

internal sealed class JobApplicationRepository(JobTrackerDbContext db) : IJobApplicationRepository
{
    public Task<JobApplication?> GetAsync(Guid id, CancellationToken ct = default) =>
        db.Applications.Include(x => x.Events.OrderBy(e => e.Date)).FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<JobApplication>> ListAsync(CancellationToken ct = default) =>
        await db.Applications.AsNoTracking().Include(x => x.Events.OrderBy(e => e.Date))
            .OrderByDescending(x => x.AppliedOn)
            .ToListAsync(ct);

    /// <summary>
    /// Есть ссылка, отклик ещё в игре и вакансия не закрыта — такие имеет смысл проверять.
    /// Фоновая проверка общая для всех: фильтр по пользователю здесь снят намеренно.
    /// </summary>
    public async Task<IReadOnlyList<JobApplication>> ListForVacancyCheckAsync(CancellationToken ct = default) =>
        await db.Applications
            .IgnoreQueryFilters([UserOwnership.Filter])
            .Where(x => x.Url != null
                        && x.VacancyStatus != VacancyStatus.Inactive
                        && x.Stage != Stage.Offer && x.Stage != Stage.Rejected && x.Stage != Stage.Silent)
            .ToListAsync(ct);

    public async Task<HashSet<string>> GetLegacyIdsAsync(CancellationToken ct = default) =>
        (await db.Applications.Where(x => x.LegacyId != null).Select(x => x.LegacyId!).ToListAsync(ct)).ToHashSet();

    public void Add(JobApplication application) => db.Applications.Add(application);

    public void Remove(JobApplication application) => db.Applications.Remove(application);
}
