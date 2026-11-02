using JobTracker.Domain.Applications;

namespace JobTracker.Application.Applications;

public sealed record ApplicationEventDto(Guid Id, DateOnly Date, string Text);

public sealed record JobApplicationDto(
    Guid Id,
    string Company,
    string? Url,
    DateOnly AppliedOn,
    string Note,
    Stage Stage,
    Stage ReachedStage,
    VacancyStatus VacancyStatus,
    DateTimeOffset? LastCheckedAt,
    DateTimeOffset? ClosedAt,
    IReadOnlyList<ApplicationEventDto> Events)
{
    public static JobApplicationDto From(JobApplication a) => new(
        a.Id, a.Company, a.Url?.Value, a.AppliedOn, a.Note, a.Stage, a.ReachedStage,
        a.VacancyStatus, a.LastCheckedAt, a.ClosedAt,
        [.. a.Events.Select(e => new ApplicationEventDto(e.Id, e.Date, e.Text))]);
}

public sealed record SaveApplicationRequest(string Company, DateOnly AppliedOn, string? Url, string? Note);

public sealed record ChangeStageRequest(Stage Stage);

public sealed record AddEventRequest(DateOnly Date, string Text);
