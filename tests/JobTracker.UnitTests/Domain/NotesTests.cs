using JobTracker.Domain.Common;
using JobTracker.Domain.Notes;

namespace JobTracker.UnitTests.Domain;

public class NotesTests
{
    private static readonly DateOnly Today = new(2026, 11, 4);

    [Fact]
    public void Idea_Done_RemembersDay_BackToWork_Clears()
    {
        var idea = Idea.Create("Outbox в pet-проекте", Today.AddDays(-5));

        idea.SetStatus(IdeaStatus.Done, Today);
        Assert.Equal(Today, idea.DoneOn);

        idea.SetStatus(IdeaStatus.Doing, Today);
        Assert.Null(idea.DoneOn);
    }

    [Theory]
    [InlineData("#q11.24", "11.24")]
    [InlineData("q11.24", "11.24")]
    [InlineData(" 11.24 ", "11.24")]
    [InlineData("", "")]
    public void Idea_QuestionReference_Normalized(string input, string expected)
    {
        var idea = Idea.Create("x", Today);

        idea.Update("x", null, null, input);

        Assert.Equal(expected, idea.Question);
    }

    [Fact]
    public void Idea_EmptyTitle_Throws()
    {
        Assert.Throws<DomainException>(() => Idea.Create("  ", Today));
    }

    [Fact]
    public void Highlight_TooShortOrNegativeStart_Throws()
    {
        Assert.Throws<DomainException>(() => Highlight.Create("07-architecture", "a", "", "", 0, HighlightColor.Yellow));
        Assert.Throws<DomainException>(() => Highlight.Create("07-architecture", "SOLID", "", "", -1, HighlightColor.Yellow));
        Assert.Throws<DomainException>(() => Highlight.Create("", "SOLID", "", "", 0, HighlightColor.Yellow));
        Assert.Throws<DomainException>(() => Highlight.Create("07-architecture", "SOLID", "", "", 0, (HighlightColor)9));
    }
}
