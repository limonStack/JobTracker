using System.ComponentModel.DataAnnotations;
using JobTracker.Domain.Knowledge;

namespace JobTracker.Application.Content;

/// <summary>Разобранные тексты ответов и их версия — хеш исходных файлов: по нему импорт понимает, что ничего не менялось.</summary>
public sealed record ContentPackage(string Version, IReadOnlyList<Section> Sections, IReadOnlyList<string> Files)
{
    public int QuestionCount => Sections.Sum(s => s.Questions.Count());
}

/// <summary>Откуда берутся тексты ответов (сейчас — HTML-файлы в папке content).</summary>
public interface IContentSource
{
    /// <summary>Null — файлов нет, импортировать нечего.</summary>
    Task<ContentPackage?> LoadAsync(CancellationToken ct);
}

public interface IContentRepository
{
    Task<string?> GetVersionAsync(CancellationToken ct);

    /// <summary>Заменяет весь контент одной транзакцией: данные пользователей ссылаются на номер вопроса, а не на строку в таблице.</summary>
    Task ReplaceAsync(ContentPackage package, DateTimeOffset importedAt, CancellationToken ct);

    /// <summary>Разделы по порядку, с группами и вопросами.</summary>
    Task<IReadOnlyList<Section>> ListAsync(CancellationToken ct);
}

/// <summary>Настройки текстов ответов — секция «Content» в appsettings.json.</summary>
public sealed class ContentOptions
{
    public const string Section = "Content";

    /// <summary>Папка с файлами относительно папки приложения (туда их копирует сборка).</summary>
    [Required]
    public string Directory { get; set; } = "content";

    /// <summary>Файлы по порядку; отсутствующие пропускаются — личные разделы есть не у всех.</summary>
    [MinLength(1)]
    public string[] Files { get; set; } = ["answers.html", "private/answers.private.html"];

    /// <summary>Обновлять базу при запуске, если файлы изменились.</summary>
    public bool ImportOnStartup { get; set; } = true;
}

public sealed record ContentDto(string Version, IReadOnlyList<SectionDto> Sections);

public sealed record SectionDto(int Number, string Slug, string Title, string IntroHtml, IReadOnlyList<GroupDto> Groups);

public sealed record GroupDto(string? Title, string IntroHtml, IReadOnlyList<QuestionDto> Questions);

public sealed record QuestionDto(string Code, string Title, Importance Importance, bool IsTrick, string BodyHtml);
