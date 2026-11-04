using JobTracker.Domain.Knowledge;
using JobTracker.Domain.Study;

namespace JobTracker.Application.Abstractions;

/// <summary>Учёба текущего пользователя: отметки, треки календаря, повторение, настройки.</summary>
public interface IStudyRepository
{
    Task<IReadOnlyList<StudyMark>> ListMarksAsync(CancellationToken ct);
    Task<StudyMark?> GetMarkAsync(QuestionCode code, CancellationToken ct);
    void Add(StudyMark mark);
    void Remove(StudyMark mark);

    Task<IReadOnlyList<ActivityDay>> ListActivitiesAsync(CancellationToken ct);
    Task<ActivityDay?> GetActivityAsync(ActivityKind kind, DateOnly day, CancellationToken ct);
    void Add(ActivityDay activity);
    void Remove(ActivityDay activity);

    Task<IReadOnlyList<ReviewMark>> ListReviewsAsync(CancellationToken ct);
    Task<ReviewMark?> GetReviewAsync(DateOnly day, QuestionCode code, CancellationToken ct);
    void Add(ReviewMark review);
    void Remove(ReviewMark review);

    /// <summary>Удаляет отметки повторения старше дня — сразу в базе, без SaveChanges.</summary>
    Task RemoveReviewsBeforeAsync(DateOnly day, CancellationToken ct);

    Task<UserSettings?> GetSettingsAsync(CancellationToken ct);
    void Add(UserSettings settings);
}

/// <summary>«Сегодня» в часовом поясе текущего пользователя.</summary>
public interface IUserClock
{
    Task<DateOnly> TodayAsync(CancellationToken ct);
}
