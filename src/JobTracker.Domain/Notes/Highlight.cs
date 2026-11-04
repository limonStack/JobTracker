using JobTracker.Domain.Common;

namespace JobTracker.Domain.Notes;

public enum HighlightColor
{
    Yellow,
    Green,
    Pink,
}

/// <summary>
/// Выделение маркером в тексте ответа. Привязано к самому тексту раздела, а не к положению в разметке:
/// интерфейс ищет <see cref="Text"/> в тексте раздела, при нескольких совпадениях — с тем же
/// <see cref="Prefix"/>/<see cref="Suffix"/>, затем ближайшее к <see cref="Start"/>. Поэтому правки
/// в других местах раздела выделение не сбивают, а если переписать сам фрагмент — оно просто не покажется.
/// </summary>
public sealed class Highlight : Entity, IUserOwned
{
    public const int TextMaxLength = 20_000;
    public const int ContextMaxLength = 200;

    /// <summary>Якорь раздела: «07-architecture».</summary>
    public string Section { get; private set; } = null!;

    public string Text { get; private set; } = null!;
    public string Prefix { get; private set; } = "";
    public string Suffix { get; private set; } = "";

    /// <summary>Позиция в тексте раздела на момент выделения — подсказка, если совпадений несколько.</summary>
    public int Start { get; private set; }

    public HighlightColor Color { get; private set; }

    public string? LegacyId { get; private set; }

    private Highlight() { } // для EF Core

    public static Highlight Create(string section, string text, string? prefix, string? suffix, int start, HighlightColor color,
        string? legacyId = null)
    {
        if (string.IsNullOrWhiteSpace(section))
            throw new DomainException("Не указан раздел выделения.");

        var highlight = new Highlight { Section = section.Trim(), LegacyId = legacyId };
        highlight.Update(text, prefix, suffix, start, color);
        return highlight;
    }

    /// <summary>Новое выделение слилось с этим или сменился цвет.</summary>
    public void Update(string text, string? prefix, string? suffix, int start, HighlightColor color)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Trim().Length < 2)
            throw new DomainException("Выделение короче двух символов.");
        if (text.Length > TextMaxLength)
            throw new DomainException($"Выделение длиннее {TextMaxLength} символов.");
        if (prefix?.Length > ContextMaxLength || suffix?.Length > ContextMaxLength)
            throw new DomainException($"Окружение выделения длиннее {ContextMaxLength} символов.");
        if (start < 0)
            throw new DomainException("Позиция выделения не может быть отрицательной.");
        if (!Enum.IsDefined(color))
            throw new DomainException($"Неизвестный цвет: {color}.");

        Text = text;
        Prefix = prefix ?? "";
        Suffix = suffix ?? "";
        Start = start;
        Color = color;
    }
}
