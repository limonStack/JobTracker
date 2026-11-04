using JobTracker.Domain.Study;

namespace JobTracker.Application.Study;

/// <summary>Day = null — изучено, но день неизвестен (перенесено из старого сайта без даты).</summary>
public sealed record StudyMarkDto(string Code, DateOnly? Day);

public sealed record ActivityDto(ActivityKind Kind, DateOnly Day);

public sealed record ReviewDto(DateOnly Day, string Code);

public sealed record SettingsDto(int DailyTheoryGoal, int WeeklyWorkoutGoal, bool? CalendarOpen, bool FocusMode, string? TimeZone)
{
    public static SettingsDto From(UserSettings s) => new(s.DailyTheoryGoal, s.WeeklyWorkoutGoal, s.CalendarOpen, s.FocusMode, s.TimeZone);
}

/// <summary>Всё о учёбе пользователя одним ответом: интерфейс сам считает прогресс, календарь и «Повторить».</summary>
public sealed record StudyStateDto(
    DateOnly Today,
    IReadOnlyList<StudyMarkDto> Marks,
    IReadOnlyList<ActivityDto> Activities,
    IReadOnlyList<ReviewDto> Reviews,
    SettingsDto Settings);

/// <summary>Отметить изученным. Day не указан — сегодня.</summary>
public sealed record MarkStudiedRequest(DateOnly? Day);
