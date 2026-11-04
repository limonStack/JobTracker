using System.Globalization;
using JobTracker.Domain.Common;

namespace JobTracker.Domain.Knowledge;

/// <summary>
/// Номер вопроса «раздел.вопрос» — «7.1». Value Object: всегда валидный; сравнивается и сортируется
/// по числам, а не как строка («7.10» после «7.9»).
/// </summary>
public readonly record struct QuestionCode(int Section, int Number) : IComparable<QuestionCode>
{
    public static QuestionCode Parse(string value) =>
        TryParse(value, out var code) ? code : throw new DomainException($"Неверный номер вопроса: «{value}».");

    public static bool TryParse(string? value, out QuestionCode code)
    {
        code = default;
        var parts = value?.Trim().Split('.');
        if (parts is not { Length: 2 }
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var section)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            || section <= 0 || number <= 0)
            return false;

        code = new QuestionCode(section, number);
        return true;
    }

    public int CompareTo(QuestionCode other) =>
        Section != other.Section ? Section.CompareTo(other.Section) : Number.CompareTo(other.Number);

    public static bool operator <(QuestionCode left, QuestionCode right) => left.CompareTo(right) < 0;
    public static bool operator <=(QuestionCode left, QuestionCode right) => left.CompareTo(right) <= 0;
    public static bool operator >(QuestionCode left, QuestionCode right) => left.CompareTo(right) > 0;
    public static bool operator >=(QuestionCode left, QuestionCode right) => left.CompareTo(right) >= 0;

    public override string ToString() => $"{Section}.{Number}";
}
