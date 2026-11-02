using JobTracker.Domain.Common;

namespace JobTracker.Domain.Applications;

/// <summary>Проверка показала, что вакансия больше не активна.</summary>
public sealed record VacancyClosedEvent(Guid ApplicationId, string Company, DateTimeOffset ClosedAt) : IDomainEvent;
