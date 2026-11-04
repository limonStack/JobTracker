using JobTracker.Domain.Common;
using JobTracker.Domain.Knowledge;
using JobTracker.Domain.Study;

namespace JobTracker.UnitTests.Domain;

public class StudyTests
{
    private static readonly DateOnly Today = new(2026, 11, 4);
    private static readonly QuestionCode Q = new(7, 1);

    [Fact]
    public void Mark_PastOrToday_Allowed()
    {
        Assert.Equal(Today, StudyMark.Create(Q, Today, Today).StudiedOn);
        Assert.Equal(Today.AddDays(-3), StudyMark.Create(Q, Today.AddDays(-3), Today).StudiedOn);
    }

    [Fact]
    public void Mark_Future_Throws()
    {
        Assert.Throws<DomainException>(() => StudyMark.Create(Q, Today.AddDays(1), Today));
        Assert.Throws<DomainException>(() => StudyMark.Create(Q, Today, Today).MoveTo(Today.AddDays(1), Today));
    }

    [Fact]
    public void Activity_Future_Throws()
    {
        Assert.Throws<DomainException>(() => ActivityDay.Create(ActivityKind.Workout, Today.AddDays(1), Today));
        Assert.Throws<DomainException>(() => ActivityDay.Create((ActivityKind)42, Today, Today));
    }

    [Fact]
    public void Review_CutoffIs30DaysBack()
    {
        Assert.Equal(new DateOnly(2026, 10, 5), ReviewMark.Cutoff(Today));
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(101, 3)]
    [InlineData(10, 0)]
    [InlineData(10, 8)]
    public void Settings_GoalsOutOfRange_Throw(int theory, int workouts)
    {
        Assert.Throws<DomainException>(() => UserSettings.CreateDefault().Update(theory, workouts, null, false, null));
    }

    [Fact]
    public void Settings_UnknownTimeZone_Throws()
    {
        Assert.Throws<DomainException>(() => UserSettings.CreateDefault().Update(10, 3, null, false, "Mars/Olympus"));
    }

    [Fact]
    public void Today_UsesUserTimeZone()
    {
        var settings = UserSettings.CreateDefault();
        settings.Update(10, 3, null, false, "Europe/Kyiv");
        // 23:30 UTC 3 ноября — в Киеве уже 01:30 4 ноября
        var now = new DateTimeOffset(2026, 11, 3, 23, 30, 0, TimeSpan.Zero);

        Assert.Equal(new DateOnly(2026, 11, 4), settings.Today(now));
    }

    [Fact]
    public void QuestionCode_SortsNumerically()
    {
        string[] raw = ["7.10", "7.2", "10.1", "7.1"];
        var codes = raw.Select(QuestionCode.Parse).Order().Select(c => c.ToString());

        Assert.Equal(["7.1", "7.2", "7.10", "10.1"], codes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("7")]
    [InlineData("7.0")]
    [InlineData("a.1")]
    [InlineData("7.1.2")]
    [InlineData("-1.2")]
    public void QuestionCode_Invalid_NotParsed(string value)
    {
        Assert.False(QuestionCode.TryParse(value, out _));
    }
}
