using JobTracker.Domain.Common;
using JobTracker.Domain.Todos;

namespace JobTracker.UnitTests.Domain;

public class TodoTests
{
    private static readonly DateOnly Today = new(2026, 11, 18);

    [Fact]
    public void Done_RemembersDay_RepeatKeepsFirstDay_UndoClears()
    {
        var todo = TodoItem.Create("Разобрать 7.6", Today.AddDays(-2));

        todo.SetDone(true, Today);
        todo.SetDone(true, Today.AddDays(1));   // повтор из очереди — день не переписывается
        Assert.Equal(Today, todo.DoneOn);

        todo.SetDone(false, Today);
        Assert.Null(todo.DoneOn);
        Assert.Equal(Today.AddDays(-2), todo.PlannedOn);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyTitle_Throws(string title)
    {
        Assert.Throws<DomainException>(() => TodoItem.Create(title, Today));
    }

    [Fact]
    public void TooLongTitle_Throws_TrimmedTitleKept()
    {
        Assert.Throws<DomainException>(() => TodoItem.Create(new string('x', TodoItem.TitleMaxLength + 1), Today));
        Assert.Equal("Позвонить", TodoItem.Create("  Позвонить ", Today).Title);
    }
}
