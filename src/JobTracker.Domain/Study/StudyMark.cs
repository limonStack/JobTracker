using JobTracker.Domain.Common;
using JobTracker.Domain.Knowledge;

namespace JobTracker.Domain.Study;

internal static class StudyRules
{
    /// <summary>Отметка — о том, что уже было: на будущий день её не поставить.</summary>
    public static void EnsureNotFuture(DateOnly day, DateOnly today)
    {
        if (day > today)
            throw new DomainException($"Нельзя отметить будущий день ({day:dd.MM.yyyy}).");
    }
}

/// <summary>
/// «Вопрос изучен» и день, на который это засчитано. Из этих отметок считаются прогресс, «Сегодня 6 / 10»
/// и цвет дня в календаре. День можно поставить прошедший — забыл отметить вчера.
/// </summary>
public sealed class StudyMark : Entity, IUserOwned
{
    public QuestionCode Code { get; private set; }

    public DateOnly StudiedOn { get; private set; }

    private StudyMark() { } // для EF Core

    public static StudyMark Create(QuestionCode code, DateOnly studiedOn, DateOnly today)
    {
        StudyRules.EnsureNotFuture(studiedOn, today);
        return new StudyMark { Code = code, StudiedOn = studiedOn };
    }

    /// <summary>Перенести на другой день — например, засчитать тему за вчера, а не за сегодня.</summary>
    public void MoveTo(DateOnly studiedOn, DateOnly today)
    {
        StudyRules.EnsureNotFuture(studiedOn, today);
        StudiedOn = studiedOn;
    }
}

/// <summary>Трек календаря, который отмечается «было / не было» по дням, отдельно от учёбы.</summary>
public enum ActivityKind
{
    /// <summary>💪 тренировка.</summary>
    Workout,

    /// <summary>🤖 занимался ИИ.</summary>
    Ai,
}

/// <summary>В этот день была тренировка или занятие ИИ.</summary>
public sealed class ActivityDay : Entity, IUserOwned
{
    public ActivityKind Kind { get; private set; }

    public DateOnly Day { get; private set; }

    private ActivityDay() { } // для EF Core

    public static ActivityDay Create(ActivityKind kind, DateOnly day, DateOnly today)
    {
        if (!Enum.IsDefined(kind))
            throw new DomainException($"Неизвестный трек: {kind}.");
        StudyRules.EnsureNotFuture(day, today);
        return new ActivityDay { Kind = kind, Day = day };
    }
}

/// <summary>
/// «Повторил» — тема из прошедшего дня учёбы перед новыми. Только для списка «🔁 Повторить»,
/// в статистику не идёт и через <see cref="KeepDays"/> дней не нужна.
/// </summary>
public sealed class ReviewMark : Entity, IUserOwned
{
    public const int KeepDays = 30;

    /// <summary>День учёбы, темы которого повторяют.</summary>
    public DateOnly Day { get; private set; }

    public QuestionCode Code { get; private set; }

    private ReviewMark() { } // для EF Core

    public static ReviewMark Create(DateOnly day, QuestionCode code, DateOnly today)
    {
        StudyRules.EnsureNotFuture(day, today);
        return new ReviewMark { Day = day, Code = code };
    }

    /// <summary>Отметки старше этого дня можно удалять.</summary>
    public static DateOnly Cutoff(DateOnly today) => today.AddDays(-KeepDays);
}
