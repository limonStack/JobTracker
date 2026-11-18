using JobTracker.Domain.Common;

namespace JobTracker.Domain.Todos;

/// <summary>
/// Дело в списке на день. Невыполненное не теряется: в списке «на сегодня» оно показывается и в следующие дни,
/// пока его не отметят, — для этого <see cref="PlannedOn"/> не переписывается, перенос считает интерфейс.
/// </summary>
public sealed class TodoItem : Entity, IUserOwned
{
    public const int TitleMaxLength = 300;

    public string Title { get; private set; } = null!;

    /// <summary>День, на который дело записали.</summary>
    public DateOnly PlannedOn { get; private set; }

    /// <summary>Когда сделано; null — ещё не сделано.</summary>
    public DateOnly? DoneOn { get; private set; }

    public bool IsDone => DoneOn is not null;

    private TodoItem() { } // для EF Core

    public static TodoItem Create(string title, DateOnly plannedOn, Guid? id = null)
    {
        var item = new TodoItem { PlannedOn = plannedOn, Id = id ?? Guid.CreateVersion7() };
        item.Rename(title);
        return item;
    }

    public void Rename(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("У дела нет текста.");
        if (title.Trim().Length > TitleMaxLength)
            throw new DomainException($"Дело длиннее {TitleMaxLength} символов.");

        Title = title.Trim();
    }

    /// <summary>Отметить сделанным в указанный день или снять отметку. Повтор того же — без изменений.</summary>
    public void SetDone(bool done, DateOnly day)
    {
        if (done == IsDone)
            return;

        DoneOn = done ? day : null;
    }
}
