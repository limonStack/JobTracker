using JobTracker.Application.Content;
using JobTracker.Domain.Knowledge;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.Infrastructure.Persistence;

/// <summary>Какая версия текстов сейчас в базе — одна строка.</summary>
internal sealed class ContentVersion
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    public string Version { get; set; } = "";
    public DateTimeOffset ImportedAt { get; set; }
    public int QuestionCount { get; set; }
}

internal sealed class ContentRepository(JobTrackerDbContext db) : IContentRepository
{
    public Task<string?> GetVersionAsync(CancellationToken ct) =>
        db.Set<ContentVersion>().Where(v => v.Id == ContentVersion.SingletonId).Select(v => v.Version).FirstOrDefaultAsync(ct);

    public async Task ReplaceAsync(ContentPackage package, DateTimeOffset importedAt, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // по порядку, от вопросов к разделам: не полагаемся на каскад — у SQLite он зависит от PRAGMA foreign_keys
        await db.Set<Question>().ExecuteDeleteAsync(ct);
        await db.Set<QuestionGroup>().ExecuteDeleteAsync(ct);
        await db.Set<Section>().ExecuteDeleteAsync(ct);
        await db.Set<ContentVersion>().ExecuteDeleteAsync(ct);

        db.Set<Section>().AddRange(package.Sections);
        db.Set<ContentVersion>().Add(new ContentVersion
        {
            Version = package.Version,
            ImportedAt = importedAt,
            QuestionCount = package.QuestionCount,
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        db.ChangeTracker.Clear();
    }

    public Task<bool> QuestionExistsAsync(QuestionCode code, CancellationToken ct) =>
        db.Set<Question>().AnyAsync(q => q.Code == code, ct);

    public async Task<HashSet<QuestionCode>> ListCodesAsync(CancellationToken ct) =>
        (await db.Set<Question>().Select(q => q.Code).ToListAsync(ct)).ToHashSet();

    public async Task<IReadOnlyList<Section>> ListAsync(CancellationToken ct)
    {
        var sections = await db.Set<Section>()
            .AsNoTracking()
            .AsSplitQuery()
            .Include(s => s.Groups.OrderBy(g => g.Order))
            .ThenInclude(g => g.Questions.OrderBy(q => q.Order))
            .OrderBy(s => s.Number)
            .ToListAsync(ct);
        return sections;
    }
}
