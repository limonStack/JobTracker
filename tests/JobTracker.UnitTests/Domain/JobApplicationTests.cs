using JobTracker.Domain.Applications;
using JobTracker.Domain.Common;

namespace JobTracker.UnitTests.Domain;

public class JobApplicationTests
{
    private static readonly DateOnly Day1 = new(2026, 9, 21);
    private static readonly DateOnly Day2 = new(2026, 9, 25);
    private static readonly DateOnly Day3 = new(2026, 10, 8);

    private static JobApplication NewApplication() => JobApplication.Create("UNITY-BARS", Day1);

    [Fact]
    public void Create_WithoutCompany_Throws()
    {
        Assert.Throws<DomainException>(() => JobApplication.Create("  ", Day1));
    }

    [Fact]
    public void Create_StartsAsAppliedWithUnknownVacancyStatus()
    {
        var app = NewApplication();

        Assert.Equal(Stage.Applied, app.Stage);
        Assert.Equal(Stage.Applied, app.ReachedStage);
        Assert.Equal(VacancyStatus.Unknown, app.VacancyStatus);
        Assert.Empty(app.Events);
    }

    [Fact]
    public void MoveTo_Forward_UpdatesReachedStageAndAddsEvent()
    {
        var app = NewApplication();

        app.MoveTo(Stage.Hr, Day2);

        Assert.Equal(Stage.Hr, app.Stage);
        Assert.Equal(Stage.Hr, app.ReachedStage);
        var e = Assert.Single(app.Events);
        Assert.Equal(Day2, e.Date);
        Assert.Equal("hr", e.Text);
    }

    [Fact]
    public void MoveTo_BackInFunnel_IsCorrection_RollsBackReachedStageWithoutEvent()
    {
        var app = NewApplication();
        app.MoveTo(Stage.Tech, Day2);

        app.MoveTo(Stage.Replied, Day3);

        Assert.Equal(Stage.Replied, app.Stage);
        Assert.Equal(Stage.Replied, app.ReachedStage);
        Assert.Single(app.Events);
    }

    [Fact]
    public void MoveTo_Rejected_KeepsReachedStage()
    {
        var app = NewApplication();
        app.MoveTo(Stage.Tech, Day2);

        app.MoveTo(Stage.Rejected, Day3);

        Assert.Equal(Stage.Rejected, app.Stage);
        Assert.Equal(Stage.Tech, app.ReachedStage);
        Assert.False(app.IsOpen);
        Assert.Equal("отказ", app.Events[^1].Text);
    }

    [Fact]
    public void MoveTo_SameStage_DoesNothing()
    {
        var app = NewApplication();

        app.MoveTo(Stage.Applied, Day2);

        Assert.Empty(app.Events);
    }

    [Fact]
    public void AddEvent_KeepsEventsSortedByDate()
    {
        var app = NewApplication();
        app.AddEvent(Day3, "собес");
        app.AddEvent(Day1, "отклик");
        app.AddEvent(Day2, "разговор с HR");

        Assert.Equal([Day1, Day2, Day3], app.Events.Select(e => e.Date));
    }

    [Fact]
    public void RemoveEvent_Unknown_Throws()
    {
        Assert.Throws<DomainException>(() => NewApplication().RemoveEvent(Guid.NewGuid()));
    }

    [Fact]
    public void RecordCheck_Inactive_SetsClosedAtAndRaisesEventOnce()
    {
        var app = NewApplication();
        var t1 = new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

        app.RecordCheck(VacancyStatus.Inactive, t1);
        app.RecordCheck(VacancyStatus.Inactive, t1.AddDays(1));

        Assert.Equal(VacancyStatus.Inactive, app.VacancyStatus);
        Assert.Equal(t1, app.ClosedAt);
        Assert.Equal(t1.AddDays(1), app.LastCheckedAt);
        Assert.Equal(2, app.Checks.Count);
        var closed = Assert.IsType<VacancyClosedEvent>(Assert.Single(app.DomainEvents));
        Assert.Equal(app.Id, closed.ApplicationId);
    }

    [Fact]
    public void RecordCheck_FailedAfterInactive_KeepsInactive()
    {
        var app = NewApplication();
        var t1 = new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);
        app.RecordCheck(VacancyStatus.Inactive, t1);

        app.RecordCheck(VacancyStatus.CheckFailed, t1.AddDays(1), "timeout");

        Assert.Equal(VacancyStatus.Inactive, app.VacancyStatus);
        Assert.Equal("timeout", app.Checks[^1].Error);
    }

    [Fact]
    public void RecordCheck_ActiveAgain_ClearsClosedAt()
    {
        var app = NewApplication();
        var t1 = new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);
        app.RecordCheck(VacancyStatus.Inactive, t1);

        app.RecordCheck(VacancyStatus.Active, t1.AddDays(1));

        Assert.Equal(VacancyStatus.Active, app.VacancyStatus);
        Assert.Null(app.ClosedAt);
    }

    [Fact]
    public void RestoreStage_FinalStage_KeepsGivenReachedStage()
    {
        var app = NewApplication();

        app.RestoreStage(Stage.Rejected, Stage.Tech);

        Assert.Equal(Stage.Rejected, app.Stage);
        Assert.Equal(Stage.Tech, app.ReachedStage);
    }
}
