using Microsoft.Extensions.Logging;

namespace JobTracker.Application.Content;

public sealed record ContentImportResult(bool Imported, string? Version, int Sections, int Questions);

/// <summary>Тексты ответов: импорт из файлов в базу и выдача для интерфейса.</summary>
public sealed partial class ContentService(
    IContentSource source,
    IContentRepository repository,
    TimeProvider time,
    ILogger<ContentService> logger)
{
    /// <summary>Импортирует, только если файлы изменились с прошлого раза — запуск приложения не ждёт лишнего разбора.</summary>
    public async Task<ContentImportResult> ImportIfChangedAsync(CancellationToken ct)
    {
        var package = await source.LoadAsync(ct);
        if (package is null)
        {
            LogNoFiles();
            return new ContentImportResult(false, await repository.GetVersionAsync(ct), 0, 0);
        }

        if (await repository.GetVersionAsync(ct) == package.Version)
            return new ContentImportResult(false, package.Version, package.Sections.Count, package.QuestionCount);

        await repository.ReplaceAsync(package, time.GetUtcNow(), ct);
        LogImported(package.Sections.Count, package.QuestionCount, package.Files);
        return new ContentImportResult(true, package.Version, package.Sections.Count, package.QuestionCount);
    }

    public Task<string?> GetVersionAsync(CancellationToken ct) => repository.GetVersionAsync(ct);

    public async Task<ContentDto?> GetAsync(CancellationToken ct)
    {
        if (await repository.GetVersionAsync(ct) is not { } version)
            return null;

        var sections = await repository.ListAsync(ct);
        return new ContentDto(version, sections.Select(s => new SectionDto(
            s.Number, s.Slug, s.Title, s.IntroHtml,
            s.Groups.Select(g => new GroupDto(
                g.Title, g.IntroHtml,
                g.Questions.Select(q => new QuestionDto(q.Code.ToString(), q.Title, q.Importance, q.IsTrick, q.BodyHtml)).ToList()
            )).ToList()
        )).ToList());
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Файлов с текстами ответов нет — база знаний не обновлена")]
    private partial void LogNoFiles();

    [LoggerMessage(Level = LogLevel.Information, Message = "Тексты ответов обновлены: разделов {Sections}, вопросов {Questions} ({Files})")]
    private partial void LogImported(int sections, int questions, IReadOnlyList<string> files);
}
