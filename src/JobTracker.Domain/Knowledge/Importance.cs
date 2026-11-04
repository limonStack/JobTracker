namespace JobTracker.Domain.Knowledge;

/// <summary>Насколько вопрос важен для собеседования — значок в конце названия.</summary>
public enum Importance
{
    /// <summary>Без значка.</summary>
    None,

    /// <summary>🔥 — спрашивают почти всегда, знать «от зубов».</summary>
    Must,

    /// <summary>⭐ — частый вопрос.</summary>
    Frequent,

    /// <summary>🎓 — уровень Senior: достаточно понимать идею.</summary>
    Senior,
}

public static class ImportanceExtensions
{
    public static string Icon(this Importance importance) => importance switch
    {
        Importance.Must => "🔥",
        Importance.Frequent => "⭐",
        Importance.Senior => "🎓",
        _ => "",
    };

    public static Importance FromIcon(string? icon) => icon switch
    {
        "🔥" => Importance.Must,
        "⭐" => Importance.Frequent,
        "🎓" => Importance.Senior,
        _ => Importance.None,
    };
}
