using System.Globalization;
using System.Text;
using JobTracker.Application.Abstractions;
using JobTracker.Domain.Notes;

namespace JobTracker.Application.Notes;

public sealed record IdeaDto(Guid Id, string Title, string Note, string Project, string Question, IdeaStatus Status,
    DateOnly CreatedOn, DateOnly? DoneOn)
{
    public static IdeaDto From(Idea x) => new(x.Id, x.Title, x.Note, x.Project, x.Question, x.Status, x.CreatedOn, x.DoneOn);
}

public sealed record CreateIdeaRequest(string Title);

/// <summary>CreatedOn — только при создании: день, когда идею записали (может быть офлайн, раньше отправки).</summary>
public sealed record UpdateIdeaRequest(string Title, string? Note, string? Project, string? Question, DateOnly? CreatedOn = null);

/// <summary>Day — когда сменили статус; для «применил» это дата применения.</summary>
public sealed record ChangeIdeaStatusRequest(IdeaStatus Status, DateOnly? Day = null);

public sealed record HighlightDto(Guid Id, string Section, string Text, string Prefix, string Suffix, int Start, HighlightColor Color)
{
    public static HighlightDto From(Highlight h) => new(h.Id, h.Section, h.Text, h.Prefix, h.Suffix, h.Start, h.Color);
}

public sealed record SaveHighlightRequest(string Section, string Text, string? Prefix, string? Suffix, int Start, HighlightColor Color);

/// <summary>Идеи для проектов и выделения в ответах. Null — запись не найдена (или чужая).</summary>
public sealed class NotesService(INotesRepository repository, IUnitOfWork unitOfWork, IUserClock clock)
{
    private static readonly IdeaStatus[] StatusOrder = [IdeaStatus.Doing, IdeaStatus.New, IdeaStatus.Done];

    /// <summary>Сначала «пробую», потом новые, применённые — в конце; внутри — свежие сверху.</summary>
    public async Task<IReadOnlyList<IdeaDto>> ListIdeasAsync(CancellationToken ct) =>
        [.. (await repository.ListIdeasAsync(ct))
            .OrderBy(x => Array.IndexOf(StatusOrder, x.Status))
            .ThenByDescending(x => x.CreatedOn)
            .ThenByDescending(x => x.Id)        // Guid v7 растёт со временем: в один день — тоже свежие сверху
            .Select(IdeaDto.From)];

    public async Task<IdeaDto> CreateIdeaAsync(CreateIdeaRequest request, CancellationToken ct)
    {
        var idea = Idea.Create(request.Title, await clock.TodayAsync(ct));
        repository.Add(idea);
        await unitOfWork.SaveChangesAsync(ct);
        return IdeaDto.From(idea);
    }

    /// <summary>Создать с id от клиента или обновить — идемпотентно, для офлайн-очереди.</summary>
    public async Task<IdeaDto> UpsertIdeaAsync(Guid id, UpdateIdeaRequest request, CancellationToken ct)
    {
        var idea = await repository.GetIdeaAsync(id, ct);
        if (idea is null)
        {
            idea = Idea.Create(request.Title, request.CreatedOn ?? await clock.TodayAsync(ct), id: id);
            repository.Add(idea);
        }

        idea.Update(request.Title, request.Note, request.Project, request.Question);
        await unitOfWork.SaveChangesAsync(ct);
        return IdeaDto.From(idea);
    }

    public Task<IdeaDto?> ChangeIdeaStatusAsync(Guid id, IdeaStatus status, DateOnly? day, CancellationToken ct) =>
        ModifyIdeaAsync(id, (x, today) => x.SetStatus(status, day ?? today), ct);

    public async Task<bool> DeleteIdeaAsync(Guid id, CancellationToken ct)
    {
        if (await repository.GetIdeaAsync(id, ct) is not { } idea)
            return false;
        repository.Remove(idea);
        await unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Все идеи одним файлом Markdown по статусам — читать глазами и резервная копия.</summary>
    public async Task<string> ExportIdeasMarkdownAsync(CancellationToken ct)
    {
        var ideas = await repository.ListIdeasAsync(ct);
        var sb = new StringBuilder().Append("# Идеи для проектов — ").AppendLine(Fmt(await clock.TodayAsync(ct))).AppendLine();
        foreach (var status in new[] { IdeaStatus.New, IdeaStatus.Doing, IdeaStatus.Done })
        {
            var list = ideas.Where(x => x.Status == status).OrderBy(x => x.CreatedOn).ToList();
            if (list.Count == 0)
                continue;

            sb.Append("## ").AppendLine(Label(status)).AppendLine();
            foreach (var x in list)
            {
                var meta = new[] { x.Project.Length > 0 ? $"проект: {x.Project}" : null, x.Question.Length > 0 ? $"вопрос {x.Question}" : null, Fmt(x.CreatedOn) };
                sb.Append("- [").Append(status == IdeaStatus.Done ? 'x' : ' ').Append("] **").Append(x.Title).Append("**")
                  .AppendLine(string.Concat(meta.Where(m => m is not null).Select(m => " · " + m)));
                foreach (var line in x.Note.Split('\n', StringSplitOptions.None).Where(_ => x.Note.Length > 0))
                    sb.Append("  ").AppendLine(line.TrimEnd('\r'));
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    public async Task<IReadOnlyList<HighlightDto>> ListHighlightsAsync(CancellationToken ct) =>
        [.. (await repository.ListHighlightsAsync(ct)).Select(HighlightDto.From)];

    public async Task<HighlightDto> CreateHighlightAsync(SaveHighlightRequest r, CancellationToken ct)
    {
        var highlight = Highlight.Create(r.Section, r.Text, r.Prefix, r.Suffix, r.Start, r.Color);
        repository.Add(highlight);
        await unitOfWork.SaveChangesAsync(ct);
        return HighlightDto.From(highlight);
    }

    /// <summary>
    /// Создать с id от клиента или обновить (смена цвета, слияние с новым выделением; раздел не меняется).
    /// Идемпотентно — для офлайн-очереди.
    /// </summary>
    public async Task<HighlightDto> UpsertHighlightAsync(Guid id, SaveHighlightRequest r, CancellationToken ct)
    {
        if (await repository.GetHighlightAsync(id, ct) is { } highlight)
        {
            highlight.Update(r.Text, r.Prefix, r.Suffix, r.Start, r.Color);
        }
        else
        {
            highlight = Highlight.Create(r.Section, r.Text, r.Prefix, r.Suffix, r.Start, r.Color, id: id);
            repository.Add(highlight);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return HighlightDto.From(highlight);
    }

    public async Task<bool> DeleteHighlightAsync(Guid id, CancellationToken ct)
    {
        if (await repository.GetHighlightAsync(id, ct) is not { } highlight)
            return false;
        repository.Remove(highlight);
        await unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    private async Task<IdeaDto?> ModifyIdeaAsync(Guid id, Action<Idea, DateOnly> change, CancellationToken ct)
    {
        if (await repository.GetIdeaAsync(id, ct) is not { } idea)
            return null;
        change(idea, await clock.TodayAsync(ct));
        await unitOfWork.SaveChangesAsync(ct);
        return IdeaDto.From(idea);
    }

    private static string Fmt(DateOnly day) => day.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    private static string Label(IdeaStatus status) => status switch
    {
        IdeaStatus.Doing => "🔧 пробую",
        IdeaStatus.Done => "✅ применил",
        _ => "💡 идея",
    };
}
