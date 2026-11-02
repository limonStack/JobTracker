using JobTracker.Application.Abstractions;
using JobTracker.Domain.Applications;

namespace JobTracker.Application.Applications;

/// <summary>
/// Сценарии работы с откликами. Сервис только загружает агрегат, вызывает его метод и сохраняет —
/// сами правила живут в <see cref="JobApplication"/>. Null означает «отклик не найден».
/// </summary>
public sealed class JobApplicationService(IJobApplicationRepository repository, IUnitOfWork unitOfWork, TimeProvider time)
{
    private DateOnly Today => DateOnly.FromDateTime(time.GetLocalNow().DateTime);

    public async Task<IReadOnlyList<JobApplicationDto>> ListAsync(CancellationToken ct) =>
        [.. (await repository.ListAsync(ct)).Select(JobApplicationDto.From)];

    public async Task<JobApplicationDto?> GetAsync(Guid id, CancellationToken ct) =>
        await repository.GetAsync(id, ct) is { } application ? JobApplicationDto.From(application) : null;

    public async Task<JobApplicationDto> CreateAsync(SaveApplicationRequest request, CancellationToken ct)
    {
        var application = JobApplication.Create(request.Company, request.AppliedOn, request.Url, request.Note);
        repository.Add(application);
        await unitOfWork.SaveChangesAsync(ct);
        return JobApplicationDto.From(application);
    }

    public Task<JobApplicationDto?> UpdateAsync(Guid id, SaveApplicationRequest request, CancellationToken ct) =>
        ModifyAsync(id, a => a.UpdateDetails(request.Company, request.AppliedOn, request.Url, request.Note), ct);

    public Task<JobApplicationDto?> ChangeStageAsync(Guid id, Stage stage, CancellationToken ct) =>
        ModifyAsync(id, a => a.MoveTo(stage, Today), ct);

    public Task<JobApplicationDto?> AddEventAsync(Guid id, AddEventRequest request, CancellationToken ct) =>
        ModifyAsync(id, a => a.AddEvent(request.Date, request.Text), ct);

    public Task<JobApplicationDto?> RemoveEventAsync(Guid id, Guid eventId, CancellationToken ct) =>
        ModifyAsync(id, a => a.RemoveEvent(eventId), ct);

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        if (await repository.GetAsync(id, ct) is not { } application)
            return false;

        repository.Remove(application);
        await unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    private async Task<JobApplicationDto?> ModifyAsync(Guid id, Action<JobApplication> change, CancellationToken ct)
    {
        if (await repository.GetAsync(id, ct) is not { } application)
            return null;

        change(application);
        await unitOfWork.SaveChangesAsync(ct);
        return JobApplicationDto.From(application);
    }
}
