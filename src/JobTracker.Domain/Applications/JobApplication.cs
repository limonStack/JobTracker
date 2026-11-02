using JobTracker.Domain.Common;

namespace JobTracker.Domain.Applications;

/// <summary>
/// Отклик на вакансию — корень агрегата: этап воронки, история событий и проверки вакансии
/// меняются только через его методы, чтобы правила воронки нельзя было обойти.
/// </summary>
public sealed class JobApplication : Entity
{
    private readonly List<ApplicationEvent> _events = [];
    private readonly List<VacancyCheck> _checks = [];

    public string Company { get; private set; } = null!;
    public VacancyUrl? Url { get; private set; }
    public DateOnly AppliedOn { get; private set; }
    public string Note { get; private set; } = "";

    public Stage Stage { get; private set; } = Stage.Applied;

    /// <summary>Самый дальний этап воронки, до которого дошли. После отказа помнит, на чём остановились.</summary>
    public Stage ReachedStage { get; private set; } = Stage.Applied;

    public VacancyStatus VacancyStatus { get; private set; } = VacancyStatus.Unknown;
    public DateTimeOffset? LastCheckedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

    /// <summary>Идентификатор записи в старом трекере (localStorage) — чтобы повторный импорт не создавал дублей.</summary>
    public string? LegacyId { get; private set; }

    public IReadOnlyList<ApplicationEvent> Events => _events;
    public IReadOnlyList<VacancyCheck> Checks => _checks;

    /// <summary>Отклик ещё в игре: нет оффера, отказа и «нет ответа».</summary>
    public bool IsOpen => Stage is not (Stage.Offer or Stage.Rejected or Stage.Silent);

    private JobApplication() { } // для EF Core

    public static JobApplication Create(string company, DateOnly appliedOn, string? url = null, string? note = null,
        string? legacyId = null)
    {
        var application = new JobApplication { LegacyId = legacyId };
        application.UpdateDetails(company, appliedOn, url, note);
        return application;
    }

    public void UpdateDetails(string company, DateOnly appliedOn, string? url, string? note)
    {
        if (string.IsNullOrWhiteSpace(company))
            throw new DomainException("Не указана компания.");

        Company = company.Trim();
        AppliedOn = appliedOn;
        Url = VacancyUrl.Create(url);
        Note = note?.Trim() ?? "";
    }

    /// <summary>
    /// Переводит отклик на этап. Шаг назад по воронке считается исправлением ошибки:
    /// дошедший этап откатывается, событие не пишется. Иначе в историю добавляется событие с названием этапа.
    /// </summary>
    public void MoveTo(Stage stage, DateOnly today)
    {
        if (stage == Stage)
            return;

        var isCorrection = stage.IsFunnel() && stage < ReachedStage;
        Stage = stage;

        if (isCorrection)
        {
            ReachedStage = stage;
            return;
        }

        if (stage.IsFunnel() && stage > ReachedStage)
            ReachedStage = stage;

        AddEvent(today, stage.Label().ToLowerInvariant());
    }

    public ApplicationEvent AddEvent(DateOnly date, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new DomainException("Пустое событие.");

        var applicationEvent = new ApplicationEvent(date, text.Trim());
        // вставка с сохранением порядка по дате: новое событие встаёт после событий того же дня
        var index = _events.FindLastIndex(e => e.Date <= date) + 1;
        _events.Insert(index, applicationEvent);
        return applicationEvent;
    }

    public void RemoveEvent(Guid eventId)
    {
        if (_events.RemoveAll(e => e.Id == eventId) == 0)
            throw new DomainException("Событие не найдено.");
    }

    /// <summary>
    /// Записывает результат проверки вакансии. Неудачная проверка не меняет известный статус —
    /// сбой сети не должен «открывать» закрытую вакансию.
    /// </summary>
    public void RecordCheck(VacancyStatus status, DateTimeOffset checkedAt, string? error = null)
    {
        _checks.Add(new VacancyCheck(checkedAt, status, error));
        LastCheckedAt = checkedAt;

        switch (status)
        {
            case VacancyStatus.Inactive when VacancyStatus != VacancyStatus.Inactive:
                VacancyStatus = VacancyStatus.Inactive;
                ClosedAt = checkedAt;
                Raise(new VacancyClosedEvent(Id, Company, checkedAt));
                break;
            case VacancyStatus.Active:
                VacancyStatus = VacancyStatus.Active;
                ClosedAt = null;
                break;
            case VacancyStatus.CheckFailed or VacancyStatus.Unknown when VacancyStatus == VacancyStatus.Unknown:
                VacancyStatus = status;
                break;
        }
    }

    /// <summary>
    /// Восстанавливает этап при импорте из старого трекера как есть, без событий:
    /// история переносится отдельно через <see cref="AddEvent"/>.
    /// </summary>
    public void RestoreStage(Stage stage, Stage reachedStage)
    {
        if (!reachedStage.IsFunnel())
            throw new DomainException("Дошедший этап должен быть этапом воронки.");

        Stage = stage;
        ReachedStage = stage.IsFunnel() ? stage : reachedStage;
    }
}
