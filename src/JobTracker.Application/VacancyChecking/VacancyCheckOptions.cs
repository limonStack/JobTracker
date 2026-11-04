using System.ComponentModel.DataAnnotations;

namespace JobTracker.Application.VacancyChecking;

/// <summary>Настройки фоновой проверки — секция «VacancyChecks» в appsettings.json (Options pattern).</summary>
public sealed class VacancyCheckOptions
{
    public const string Section = "VacancyChecks";

    /// <summary>Проверять ли по таймеру раз в <see cref="Interval"/>. Выключено — только по кнопке «Проверить все».</summary>
    public bool ScheduleEnabled { get; set; } = true;

    /// <summary>Как часто проверять все вакансии автоматически.</summary>
    [Range(typeof(TimeSpan), "00:01:00", "7.00:00:00")]
    public TimeSpan Interval { get; set; } = TimeSpan.FromDays(1);

    /// <summary>Пауза между запросами к сайту — чтобы не нагружать его и не попасть под защиту от ботов.</summary>
    [Range(typeof(TimeSpan), "00:00:00", "00:01:00")]
    public TimeSpan DelayBetweenRequests { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Проверить все вакансии сразу при запуске приложения.</summary>
    public bool CheckOnStartup { get; set; } = true;
}
