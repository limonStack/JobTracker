namespace JobTracker.Domain.Applications;

/// <summary>Открыта ли ещё вакансия на сайте — по результату последней проверки.</summary>
public enum VacancyStatus
{
    /// <summary>Ещё не проверяли или сайт не поддерживается.</summary>
    Unknown,
    Active,
    Inactive,
    /// <summary>Проверить не удалось: сеть, 404, капча, изменилась разметка.</summary>
    CheckFailed,
}
