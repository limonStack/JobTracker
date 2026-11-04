using JobTracker.Domain.Notes;

namespace JobTracker.Application.Abstractions;

/// <summary>Идеи и выделения текущего пользователя.</summary>
public interface INotesRepository
{
    Task<IReadOnlyList<Idea>> ListIdeasAsync(CancellationToken ct);
    Task<Idea?> GetIdeaAsync(Guid id, CancellationToken ct);
    void Add(Idea idea);
    void Remove(Idea idea);

    Task<IReadOnlyList<Highlight>> ListHighlightsAsync(CancellationToken ct);
    Task<Highlight?> GetHighlightAsync(Guid id, CancellationToken ct);
    void Add(Highlight highlight);
    void Remove(Highlight highlight);

    /// <summary>Id из старого сайта, уже перенесённые, — для идемпотентного импорта.</summary>
    Task<HashSet<string>> GetIdeaLegacyIdsAsync(CancellationToken ct);
    Task<HashSet<string>> GetHighlightLegacyIdsAsync(CancellationToken ct);
}
