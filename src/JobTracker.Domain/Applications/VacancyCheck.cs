using JobTracker.Domain.Common;

namespace JobTracker.Domain.Applications;

/// <summary>Одна проверка вакансии на сайте — история, по которой видно, когда она закрылась.</summary>
public sealed class VacancyCheck : Entity
{
    public DateTimeOffset CheckedAt { get; private set; }
    public VacancyStatus Status { get; private set; }
    public string? Error { get; private set; }

    internal VacancyCheck(DateTimeOffset checkedAt, VacancyStatus status, string? error)
    {
        CheckedAt = checkedAt;
        Status = status;
        Error = error;
    }
}
