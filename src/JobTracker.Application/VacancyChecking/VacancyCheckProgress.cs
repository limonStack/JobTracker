using JobTracker.Domain.Applications;

namespace JobTracker.Application.VacancyChecking;

/// <summary>Результат проверки одной вакансии в журнале обхода.</summary>
public sealed record VacancyCheckLogEntry(DateTimeOffset At, Guid ApplicationId, string Company, VacancyStatus Status, string? Error);

/// <summary>
/// Ход обхода для интерфейса: сколько всего, сколько готово, что проверяется сейчас, журнал текущего
/// (или последнего) обхода. <see cref="Log"/> — от старых к новым.
/// </summary>
public sealed record VacancyCheckStatus(
    bool Running,
    int Total,
    int Done,
    string? Current,
    DateTimeOffset? StartedAt,
    DateTimeOffset? LastFinishedAt,
    CheckAllResult? LastResult,
    IReadOnlyList<VacancyCheckLogEntry> Log);

/// <summary>
/// Ход фонового обхода. Пишет воркер, читают запросы к API: каждое изменение — новый неизменяемый снимок,
/// поэтому читателю не нужна блокировка, а писатель один.
/// </summary>
public sealed class VacancyCheckProgress
{
    private volatile VacancyCheckStatus _status = new(false, 0, 0, null, null, null, null, []);

    public VacancyCheckStatus Status => _status;

    /// <summary>Новый обход: журнал прошлого очищается, итог прошлого остаётся до конца нового.</summary>
    public void Started(DateTimeOffset at) =>
        _status = _status with { Running = true, Total = 0, Done = 0, Current = null, StartedAt = at, Log = [] };

    public void Planned(int total) => _status = _status with { Total = total };

    public void Checking(string company) => _status = _status with { Current = company };

    public void Checked(VacancyCheckLogEntry entry) =>
        _status = _status with { Done = _status.Done + 1, Current = null, Log = [.. _status.Log, entry] };

    public void Finished(DateTimeOffset at, CheckAllResult? result) =>
        _status = _status with { Running = false, Current = null, LastFinishedAt = at, LastResult = result };
}
