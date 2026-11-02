using JobTracker.Domain.Applications;

namespace JobTracker.Application.Abstractions;

/// <summary>Хранилище агрегата JobApplication. Сохранение — через <see cref="IUnitOfWork"/>.</summary>
public interface IJobApplicationRepository
{
    Task<JobApplication?> GetAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<JobApplication>> ListAsync(CancellationToken ct = default);
    Task<IReadOnlyList<JobApplication>> ListForVacancyCheckAsync(CancellationToken ct = default);
    Task<HashSet<string>> GetLegacyIdsAsync(CancellationToken ct = default);
    void Add(JobApplication application);
    void Remove(JobApplication application);
}
