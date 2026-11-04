using JobTracker.Domain.Common;

namespace JobTracker.Domain.Study;

/// <summary>
/// Настройки пользователя: цели календаря, вид интерфейса и часовой пояс. Хранятся в базе, а не в браузере,
/// чтобы быть одинаковыми на всех устройствах. Одна запись на пользователя.
/// </summary>
public sealed class UserSettings : Entity, IUserOwned
{
    public const int DefaultDailyTheoryGoal = 10;
    public const int DefaultWeeklyWorkoutGoal = 3;

    /// <summary>📚 Новых тем в день.</summary>
    public int DailyTheoryGoal { get; private set; } = DefaultDailyTheoryGoal;

    /// <summary>💪 Тренировок в неделю.</summary>
    public int WeeklyWorkoutGoal { get; private set; } = DefaultWeeklyWorkoutGoal;

    /// <summary>Календарь открыт. Null — ещё не выбирали: интерфейс решает по ширине экрана.</summary>
    public bool? CalendarOpen { get; private set; }

    /// <summary>🎯 Режим собеседования: скрыты отметки, прогресс и календарь.</summary>
    public bool FocusMode { get; private set; }

    /// <summary>
    /// IANA-зона («Europe/Kyiv»): по ней считается «сегодня». Сервер в облаке живёт в UTC, и без зоны
    /// отметка, поставленная поздно вечером, ушла бы на завтра. Null — зона ещё не известна.
    /// </summary>
    public string? TimeZone { get; private set; }

    public static UserSettings CreateDefault() => new();

    public void Update(int dailyTheoryGoal, int weeklyWorkoutGoal, bool? calendarOpen, bool focusMode, string? timeZone)
    {
        if (dailyTheoryGoal is < 1 or > 100)
            throw new DomainException("Цель по теории — от 1 до 100 тем в день.");
        if (weeklyWorkoutGoal is < 1 or > 7)
            throw new DomainException("Цель по тренировкам — от 1 до 7 в неделю.");

        DailyTheoryGoal = dailyTheoryGoal;
        WeeklyWorkoutGoal = weeklyWorkoutGoal;
        CalendarOpen = calendarOpen;
        FocusMode = focusMode;
        TimeZone = NormalizeTimeZone(timeZone);
    }

    /// <summary>Сегодняшний день пользователя: в его часовом поясе, а пока он не известен — в поясе сервера.</summary>
    public DateOnly Today(DateTimeOffset now)
    {
        var zone = TimeZone is null ? TimeZoneInfo.Local : TimeZoneInfo.FindSystemTimeZoneById(TimeZone);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
    }

    private static string? NormalizeTimeZone(string? timeZone)
    {
        if (string.IsNullOrWhiteSpace(timeZone))
            return null;
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZone.Trim()).Id;
        }
        catch (TimeZoneNotFoundException)
        {
            throw new DomainException($"Неизвестный часовой пояс: «{timeZone}».");
        }
    }
}
