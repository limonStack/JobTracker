using JobTracker.Domain.Common;
using JobTracker.Domain.Knowledge;
using JobTracker.Infrastructure.Content;

namespace JobTracker.UnitTests.Content;

public class AnswersHtmlParserTests
{
    private const string Sample = """
        <!doctype html><html><body><main>
        <section><h1 id="05-linq">5. LINQ</h1>
        <p>Вступление раздела.</p>
        <h2>LINQ</h2>
        <h3 id="q5.1"><a class="anchor" href="#q5.1">#</a>5.1 Что такое LINQ ⭐</h3>
        <p>Ответ 5.1</p><pre><code>var x = 1;</code></pre>
        <h3 id="q5.2"><a class="anchor" href="#q5.2">#</a>5.2 Отложенное выполнение 🔥</h3>
        <p>Ответ 5.2</p><h4>Подробно</h4><p>ещё</p>
        <h2>Подвохи</h2>
        <p>Задачи «что выведет код».</p>
        <h3 id="q5.10"><a class="anchor" href="#q5.10">#</a>5.10 Подвох: повторный перебор 🎓</h3>
        <p>Ответ 5.10</p>
        </section><section><h1 id="13-testing">13. Тестирование</h1>
        <h3 id="q13.1"><a class="anchor" href="#q13.1">#</a>13.1 Зачем нужны unit-тесты</h3>
        <p>Ответ 13.1</p>
        </section><section><h1 id="22-cheatsheet">22. Шпаргалка</h1>
        <h3>Не вопрос — обычный подзаголовок</h3><p>Шпаргалка целиком.</p>
        </section>
        </main></body></html>
        """;

    private static IReadOnlyList<Section> Parse() => AnswersHtmlParser.Parse(Sample);

    [Fact]
    public void Sections_NumberSlugTitle()
    {
        var sections = Parse();

        Assert.Equal([5, 13, 22], sections.Select(s => s.Number));
        Assert.Equal(("05-linq", "LINQ"), (sections[0].Slug, sections[0].Title));
        Assert.Contains("Вступление раздела", sections[0].IntroHtml, StringComparison.Ordinal);
    }

    [Fact]
    public void Groups_WithTitlesAndIntro()
    {
        var groups = Parse()[0].Groups;

        Assert.Equal(["LINQ", "Подвохи"], groups.Select(g => g.Title));
        Assert.Equal("", groups[0].IntroHtml);
        Assert.Contains("что выведет код", groups[1].IntroHtml, StringComparison.Ordinal);
    }

    [Fact]
    public void Question_CodeTitleImportanceBody()
    {
        var q = Parse()[0].Questions.First();

        Assert.Equal(new QuestionCode(5, 1), q.Code);
        Assert.Equal("Что такое LINQ", q.Title);      // без номера, «#» и значка
        Assert.Equal(Importance.Frequent, q.Importance);
        Assert.Contains("<pre><code>var x = 1;</code></pre>", q.BodyHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("5.2", q.BodyHtml, StringComparison.Ordinal);
    }

    [Fact]
    public void Question_BodyKeepsInnerHeadings()
    {
        var q = Parse()[0].Questions.Single(x => x.Code == new QuestionCode(5, 2));

        Assert.Equal(Importance.Must, q.Importance);
        Assert.Contains("<h4>Подробно</h4>", q.BodyHtml, StringComparison.Ordinal);
    }

    [Fact]
    public void Trick_DetectedByTitle()
    {
        var q = Parse()[0].Questions.Last();

        Assert.True(q.IsTrick);
        Assert.Equal(Importance.Senior, q.Importance);
        Assert.Equal(2, q.GroupOrder);
    }

    [Fact]
    public void SectionWithoutSubheadings_QuestionsInUntitledGroup()
    {
        var testing = Parse()[1];

        var group = Assert.Single(testing.Groups);
        Assert.Null(group.Title);
        Assert.Equal(Importance.None, Assert.Single(group.Questions).Importance);
    }

    [Fact]
    public void SectionWithoutQuestions_EverythingInIntro()
    {
        var cheatsheet = Parse()[2];

        Assert.Empty(cheatsheet.Groups);
        Assert.Contains("Шпаргалка целиком", cheatsheet.IntroHtml, StringComparison.Ordinal);
        Assert.Contains("Не вопрос", cheatsheet.IntroHtml, StringComparison.Ordinal);   // h3 без id — часть текста
    }

    [Fact]
    public void DuplicateQuestion_Throws()
    {
        var html = Sample.Replace("q5.2", "q5.1", StringComparison.Ordinal);

        Assert.Throws<DomainException>(() => AnswersHtmlParser.Parse(html));
    }

    [Fact]
    public void QuestionFromAnotherSection_Throws()
    {
        var html = Sample.Replace("q13.1", "q14.1", StringComparison.Ordinal);

        Assert.Throws<DomainException>(() => AnswersHtmlParser.Parse(html));
    }
}
