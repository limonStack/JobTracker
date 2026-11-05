using JobTracker.Domain.Common;

namespace JobTracker.Domain.Notes;

public enum IdeaStatus
{
    /// <summary>💡 идея.</summary>
    New,

    /// <summary>🔧 пробую.</summary>
    Doing,

    /// <summary>✅ применил.</summary>
    Done,
}

/// <summary>
/// Идея для проекта: прочитал ответ — захотелось применить на практике. Проект и номер вопроса —
/// свободный текст: вопрос «11.24» интерфейс покажет ссылкой, если такой есть.
/// </summary>
public sealed class Idea : Entity, IUserOwned
{
    public const int TitleMaxLength = 300;
    public const int NoteMaxLength = 5000;

    public string Title { get; private set; } = null!;
    public string Note { get; private set; } = "";
    public string Project { get; private set; } = "";
    public string Question { get; private set; } = "";
    public IdeaStatus Status { get; private set; } = IdeaStatus.New;
    public DateOnly CreatedOn { get; private set; }

    /// <summary>Когда перешла в «применил»; при возврате в работу сбрасывается.</summary>
    public DateOnly? DoneOn { get; private set; }

    /// <summary>Id в старом сайте (localStorage) — повторный импорт не создаёт дублей.</summary>
    public string? LegacyId { get; private set; }

    private Idea() { } // для EF Core

    public static Idea Create(string title, DateOnly createdOn, string? legacyId = null, Guid? id = null)
    {
        var idea = new Idea { CreatedOn = createdOn, LegacyId = legacyId, Id = id ?? Guid.CreateVersion7() };
        idea.Update(title, "", "", "");
        return idea;
    }

    public void Update(string title, string? note, string? project, string? question)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("У идеи нет текста.");
        if (title.Trim().Length > TitleMaxLength)
            throw new DomainException($"Идея длиннее {TitleMaxLength} символов — подробности лучше в «Как применить».");
        if (note?.Length > NoteMaxLength)
            throw new DomainException($"«Как применить» длиннее {NoteMaxLength} символов.");

        Title = title.Trim();
        Note = note?.TrimEnd() ?? "";
        Project = project?.Trim() ?? "";
        // «#q11.24», «q11.24», «11.24» — всё это номер 11.24
        Question = (question ?? "").Trim().TrimStart('#').TrimStart('q', 'Q');
    }

    public void SetStatus(IdeaStatus status, DateOnly today)
    {
        if (!Enum.IsDefined(status))
            throw new DomainException($"Неизвестный статус идеи: {status}.");
        if (status == Status)
            return;

        Status = status;
        DoneOn = status == IdeaStatus.Done ? today : null;
    }

    /// <summary>Перенос из старого сайта как есть, с датой «применил».</summary>
    public void RestoreStatus(IdeaStatus status, DateOnly? doneOn)
    {
        Status = Enum.IsDefined(status) ? status : IdeaStatus.New;
        DoneOn = Status == IdeaStatus.Done ? doneOn : null;
    }
}
