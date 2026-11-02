namespace JobTracker.Domain.Applications;

/// <summary>Этап отклика. Первые пять — воронка по порядку, Rejected и Silent — финальные исходы.</summary>
public enum Stage
{
    Applied,
    Replied,
    Hr,
    Tech,
    Offer,
    Rejected,
    Silent,
}

public static class StageExtensions
{
    public static bool IsFunnel(this Stage stage) => stage <= Stage.Offer;

    public static string Label(this Stage stage) => stage switch
    {
        Stage.Applied => "Отклик",
        Stage.Replied => "Ответили",
        Stage.Hr => "HR",
        Stage.Tech => "Тех. собес",
        Stage.Offer => "Оффер",
        Stage.Rejected => "Отказ",
        Stage.Silent => "Нет ответа",
        _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, null),
    };
}
