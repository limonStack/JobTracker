using JobTracker.Application.Abstractions;
using JobTracker.Application.Content;
using JobTracker.Domain.Common;
using JobTracker.Domain.Knowledge;
using JobTracker.Domain.Study;

namespace JobTracker.Application.Study;

/// <summary>
/// Прогресс, календарь и повторение. Операции идемпотентны (PUT — поставить, DELETE — снять):
/// повторный запрос из-за сети не переключит отметку обратно, как делал бы «toggle».
/// </summary>
public sealed class StudyService(
    IStudyRepository repository,
    IContentRepository content,
    IUserClock clock,
    IUnitOfWork unitOfWork)
{
    public async Task<StudyStateDto> GetStateAsync(CancellationToken ct)
    {
        var settings = await repository.GetSettingsAsync(ct) ?? UserSettings.CreateDefault();
        return new StudyStateDto(
            await clock.TodayAsync(ct),
            [.. (await repository.ListMarksAsync(ct)).OrderBy(m => m.Code).Select(m => new StudyMarkDto(m.Code.ToString(), m.StudiedOn))],
            [.. (await repository.ListActivitiesAsync(ct)).OrderBy(a => a.Day).Select(a => new ActivityDto(a.Kind, a.Day))],
            [.. (await repository.ListReviewsAsync(ct)).OrderBy(r => r.Day).Select(r => new ReviewDto(r.Day, r.Code.ToString()))],
            SettingsDto.From(settings));
    }

    /// <summary>Отметить вопрос изученным (или перенести отметку на другой день). Null — такого вопроса нет.</summary>
    public async Task<StudyMarkDto?> MarkStudiedAsync(string code, DateOnly? day, CancellationToken ct)
    {
        if (await FindQuestionAsync(code, ct) is not { } questionCode)
            return null;

        var today = await clock.TodayAsync(ct);
        var studiedOn = day ?? today;
        var mark = await repository.GetMarkAsync(questionCode, ct);
        if (mark is null)
        {
            mark = StudyMark.Create(questionCode, studiedOn, today);
            repository.Add(mark);
        }
        else
        {
            mark.MoveTo(studiedOn, today);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return new StudyMarkDto(mark.Code.ToString(), mark.StudiedOn);
    }

    public async Task UnmarkStudiedAsync(string code, CancellationToken ct)
    {
        if (!QuestionCode.TryParse(code, out var questionCode))
            return;
        if (await repository.GetMarkAsync(questionCode, ct) is { } mark)
        {
            repository.Remove(mark);
            await unitOfWork.SaveChangesAsync(ct);
        }
    }

    public async Task SetActivityAsync(ActivityKind kind, DateOnly day, bool done, CancellationToken ct)
    {
        var existing = await repository.GetActivityAsync(kind, day, ct);
        if (done && existing is null)
            repository.Add(ActivityDay.Create(kind, day, await clock.TodayAsync(ct)));
        else if (!done && existing is not null)
            repository.Remove(existing);
        else
            return;

        await unitOfWork.SaveChangesAsync(ct);
    }

    /// <summary>Отметить тему дня повторённой или снять отметку. Заодно удаляет отметки старше 30 дней. False — такого вопроса нет.</summary>
    public async Task<bool> SetReviewedAsync(DateOnly day, string code, bool reviewed, CancellationToken ct)
    {
        if (await FindQuestionAsync(code, ct) is not { } questionCode)
            return false;

        var today = await clock.TodayAsync(ct);
        var existing = await repository.GetReviewAsync(day, questionCode, ct);
        if (reviewed && existing is null)
            repository.Add(ReviewMark.Create(day, questionCode, today));
        else if (!reviewed && existing is not null)
            repository.Remove(existing);

        await repository.RemoveReviewsBeforeAsync(ReviewMark.Cutoff(today), ct);
        await unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    public async Task<SettingsDto> GetSettingsAsync(CancellationToken ct) =>
        SettingsDto.From(await repository.GetSettingsAsync(ct) ?? UserSettings.CreateDefault());

    public async Task<SettingsDto> UpdateSettingsAsync(SettingsDto request, CancellationToken ct)
    {
        var settings = await repository.GetSettingsAsync(ct);
        if (settings is null)
        {
            settings = UserSettings.CreateDefault();
            repository.Add(settings);
        }

        settings.Update(request.DailyTheoryGoal, request.WeeklyWorkoutGoal, request.CalendarOpen, request.FocusMode, request.TimeZone);
        await unitOfWork.SaveChangesAsync(ct);
        return SettingsDto.From(settings);
    }

    /// <summary>Номер вопроса, если он есть в базе знаний. Неверный формат номера — ошибка клиента.</summary>
    private async Task<QuestionCode?> FindQuestionAsync(string code, CancellationToken ct)
    {
        if (!QuestionCode.TryParse(code, out var questionCode))
            throw new DomainException($"Неверный номер вопроса: «{code}».");
        return await content.QuestionExistsAsync(questionCode, ct) ? questionCode : null;
    }
}

/// <summary>«Сегодня» по часовому поясу из настроек пользователя; настройки читаются один раз за запрос.</summary>
public sealed class UserClock(IStudyRepository repository, TimeProvider time) : IUserClock
{
    private DateOnly? _today;

    public async Task<DateOnly> TodayAsync(CancellationToken ct)
    {
        if (_today is { } cached)
            return cached;

        var settings = await repository.GetSettingsAsync(ct) ?? UserSettings.CreateDefault();
        _today = settings.Today(time.GetUtcNow());
        return _today.Value;
    }
}
