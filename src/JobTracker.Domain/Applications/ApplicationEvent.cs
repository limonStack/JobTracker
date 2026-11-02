using JobTracker.Domain.Common;

namespace JobTracker.Domain.Applications;

/// <summary>Событие в истории отклика: «разговор с HR», «отказ» и т. п.</summary>
public sealed class ApplicationEvent : Entity
{
    public DateOnly Date { get; private set; }
    public string Text { get; private set; }

    internal ApplicationEvent(DateOnly date, string text)
    {
        Date = date;
        Text = text;
    }
}
