namespace JobTracker.Domain.Common;

/// <summary>Нарушено бизнес-правило модели.</summary>
public sealed class DomainException(string message) : Exception(message);
