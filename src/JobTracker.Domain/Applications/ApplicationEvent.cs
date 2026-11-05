using JobTracker.Domain.Common;

namespace JobTracker.Domain.Applications;

/// <summary>Событие в истории отклика: «разговор с HR», «отказ» и т. п.</summary>
public sealed class ApplicationEvent : Entity
{
    public DateOnly Date { get; private set; }
    public string Text { get; private set; }

    // этот конструктор использует и EF Core: параметры совпадают со свойствами
    internal ApplicationEvent(DateOnly date, string text)
    {
        Date = date;
        Text = text;
    }

    /// <summary>С id от клиента — событие создано офлайн в браузере.</summary>
    internal ApplicationEvent(DateOnly date, string text, Guid? id) : this(date, text)
    {
        if (id is { } given)
            Id = given;
    }
}
