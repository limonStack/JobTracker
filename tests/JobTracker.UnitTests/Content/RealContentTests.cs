using JobTracker.Domain.Knowledge;
using JobTracker.Infrastructure.Content;

namespace JobTracker.UnitTests.Content;

/// <summary>
/// Разбор настоящего content/answers.html: ловит правки текста, после которых вопрос потерялся бы при импорте
/// (сломанный id, номер не из своего раздела, дубль). Числа — на момент переноса из 300questions.
/// </summary>
public class RealContentTests
{
    private static readonly Lazy<IReadOnlyList<Section>> Sections = new(() =>
        AnswersHtmlParser.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "content", "answers.html"))));

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "JobTracker.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Не найден корень репозитория (JobTracker.slnx).");
    }

    [Fact]
    public void PublicFile_HasAllPublicSections_WithoutPrivate()
    {
        var numbers = Sections.Value.Select(s => s.Number).ToList();

        Assert.Equal([.. Enumerable.Range(1, 19), 22], numbers);
    }

    [Fact]
    public void PublicFile_Has328Questions_NumberedWithinSections()
    {
        var questions = Sections.Value.SelectMany(s => s.Questions).ToList();

        Assert.Equal(328, questions.Count);
        Assert.All(questions, q => Assert.False(string.IsNullOrWhiteSpace(q.BodyHtml), $"пустой ответ {q.Code}"));
        // номера в разделе — 1..N без пропусков
        foreach (var section in Sections.Value)
            Assert.Equal(Enumerable.Range(1, section.Questions.Count()), section.Questions.Select(q => q.Code.Number));
    }

    [Fact]
    public void EveryQuestion_HasImportanceMark()
    {
        var withoutMark = Sections.Value.SelectMany(s => s.Questions).Where(q => q.Importance == Importance.None).Select(q => q.Code.ToString());

        Assert.Empty(withoutMark);
    }

    [Fact]
    public void Titles_HaveNoLeftovers()
    {
        foreach (var q in Sections.Value.SelectMany(s => s.Questions))
        {
            // «#» якоря и номер вопроса не должны попасть в начало названия («C#» в середине — нормально)
            Assert.False(q.Title.StartsWith('#'), q.Title);
            Assert.False(q.Title.StartsWith($"{q.Code} ", StringComparison.Ordinal), q.Title);
            Assert.DoesNotMatch(@"(🔥|⭐|🎓)\s*$", q.Title);
        }
    }
}
