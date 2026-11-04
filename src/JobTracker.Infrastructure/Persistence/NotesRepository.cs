using JobTracker.Application.Abstractions;
using JobTracker.Domain.Notes;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.Infrastructure.Persistence;

internal sealed class NotesRepository(JobTrackerDbContext db) : INotesRepository
{
    public async Task<IReadOnlyList<Idea>> ListIdeasAsync(CancellationToken ct) =>
        await db.Set<Idea>().AsNoTracking().ToListAsync(ct);

    public Task<Idea?> GetIdeaAsync(Guid id, CancellationToken ct) =>
        db.Set<Idea>().FirstOrDefaultAsync(x => x.Id == id, ct);

    public void Add(Idea idea) => db.Add(idea);

    public void Remove(Idea idea) => db.Remove(idea);

    public async Task<IReadOnlyList<Highlight>> ListHighlightsAsync(CancellationToken ct) =>
        await db.Set<Highlight>().AsNoTracking().ToListAsync(ct);

    public Task<Highlight?> GetHighlightAsync(Guid id, CancellationToken ct) =>
        db.Set<Highlight>().FirstOrDefaultAsync(x => x.Id == id, ct);

    public void Add(Highlight highlight) => db.Add(highlight);

    public void Remove(Highlight highlight) => db.Remove(highlight);

    public async Task<HashSet<string>> GetIdeaLegacyIdsAsync(CancellationToken ct) =>
        (await db.Set<Idea>().Where(x => x.LegacyId != null).Select(x => x.LegacyId!).ToListAsync(ct)).ToHashSet();

    public async Task<HashSet<string>> GetHighlightLegacyIdsAsync(CancellationToken ct) =>
        (await db.Set<Highlight>().Where(x => x.LegacyId != null).Select(x => x.LegacyId!).ToListAsync(ct)).ToHashSet();
}
