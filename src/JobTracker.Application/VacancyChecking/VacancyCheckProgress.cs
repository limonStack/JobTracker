namespace JobTracker.Application.VacancyChecking;

public sealed record VacancyCheckStatus(bool Running, DateTimeOffset? LastFinishedAt, CheckAllResult? LastResult);

/// <summary>Идёт ли сейчас обход и чем закончился прошлый — для кнопки «Проверить все» в интерфейсе.</summary>
public sealed class VacancyCheckProgress
{
    private volatile VacancyCheckStatus _status = new(false, null, null);

    public VacancyCheckStatus Status => _status;

    public void Started() => _status = _status with { Running = true };

    public void Finished(DateTimeOffset at, CheckAllResult? result) => _status = new VacancyCheckStatus(false, at, result);
}
