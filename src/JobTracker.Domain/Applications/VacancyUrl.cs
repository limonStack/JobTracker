namespace JobTracker.Domain.Applications;

/// <summary>Ссылка на вакансию: всегда абсолютный http(s)-адрес.</summary>
public sealed record VacancyUrl
{
    public string Value { get; }

    private VacancyUrl(string value) => Value = value;

    public Uri ToUri() => new(Value);

    public override string ToString() => Value;

    /// <summary>
    /// Ссылку без протокола («jobs.dou.ua/…») дополняет https://. Пустая строка — ссылки нет.
    /// </summary>
    public static VacancyUrl? Create(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var value = raw.Trim();
        if (!value.Contains("://", StringComparison.Ordinal))
            value = "https://" + value;

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new Common.DomainException($"Некорректная ссылка на вакансию: {raw}");

        return new VacancyUrl(uri.ToString());
    }
}
