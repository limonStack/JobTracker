using JobTracker.Application.Abstractions;
using JobTracker.Domain.Applications;

namespace JobTracker.Application.Applications;

/// <summary>
/// Сценарии работы с откликами. Сервис только загружает агрегат, вызывает его метод и сохраняет —
/// сами правила живут в <see cref="JobApplication"/>. Null означает «отклик не найден».
/// </summary>
public sealed class JobApplicationService(IJobApplicationRepository repository, IUnitOfWork unitOfWork, IUserClock clock)
{
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

    /// <summary>
    /// Создать с id от клиента или обновить. Идемпотентно: офлайн-очередь может отправить тот же запрос
    /// повторно после обрыва связи — дубля не будет.
    /// </summary>
    public async Task<JobApplicationDto> UpsertAsync(Guid id, SaveApplicationRequest request, CancellationToken ct)
    {
        if (await repository.GetAsync(id, ct) is { } existing)
        {
            existing.UpdateDetails(request.Company, request.AppliedOn, request.Url, request.Note);
            await unitOfWork.SaveChangesAsync(ct);
            return JobApplicationDto.From(existing);
        }

        var application = JobApplication.Create(request.Company, request.AppliedOn, request.Url, request.Note, id: id);
        repository.Add(application);
        await unitOfWork.SaveChangesAsync(ct);
        return JobApplicationDto.From(application);
    }

    /// <summary>Событие о смене этапа датируется днём пользователя: переданным клиентом или сегодняшним.</summary>
    public async Task<JobApplicationDto?> ChangeStageAsync(Guid id, Stage stage, DateOnly? day, CancellationToken ct)
    {
        var date = day ?? await clock.TodayAsync(ct);
        return await ModifyAsync(id, a => a.MoveTo(stage, date), ct);
    }

    public Task<JobApplicationDto?> AddEventAsync(Guid id, AddEventRequest request, CancellationToken ct, Guid? eventId = null) =>
        ModifyAsync(id, a => a.AddEvent(request.Date, request.Text, eventId), ct);

    // уже удалённое событие — не ошибка: повтор удаления из офлайн-очереди
    public Task<JobApplicationDto?> RemoveEventAsync(Guid id, Guid eventId, CancellationToken ct) =>
        ModifyAsync(id, a => { if (a.Events.Any(e => e.Id == eventId)) a.RemoveEvent(eventId); }, ct);

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
