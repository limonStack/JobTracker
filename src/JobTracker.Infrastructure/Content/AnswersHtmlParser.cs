using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using JobTracker.Domain.Common;
using JobTracker.Domain.Knowledge;

namespace JobTracker.Infrastructure.Content;

/// <summary>
/// Разбирает страницу ответов (формат ANSWERS.html из 300questions) на разделы, группы и вопросы.
/// Внутри <c>main &gt; section</c>: h1 — раздел, h2 — группа, h3 с id «qN.M» — вопрос;
/// всё между заголовками — HTML вступления или ответа, переносится как есть.
/// </summary>
public static partial class AnswersHtmlParser
{
    /// <summary>Меняется вместе с логикой разбора: тогда тексты переимпортируются, даже если файлы те же.</summary>
    public const string Version = "1";

    private static readonly HtmlParser Parser = new();

    public static IReadOnlyList<Section> Parse(string html)
    {
        using var document = Parser.ParseDocument(html);
        var sections = document.QuerySelectorAll("main > section").Select(ParseSection).ToList();

        var duplicate = sections.SelectMany(s => s.Questions).GroupBy(q => q.Code).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new DomainException($"Вопрос {duplicate.Key} встречается дважды.");
        var duplicateSection = sections.GroupBy(s => s.Number).FirstOrDefault(g => g.Count() > 1);
        if (duplicateSection is not null)
            throw new DomainException($"Раздел {duplicateSection.Key} встречается дважды.");

        return sections;
    }

    private static Section ParseSection(IElement element)
    {
        var h1 = element.Children.FirstOrDefault(c => c.LocalName == "h1")
                 ?? throw new DomainException("Раздел без заголовка h1.");
        var heading = SectionHeading().Match(h1.TextContent.Trim());
        if (!heading.Success)
            throw new DomainException($"Заголовок раздела не по формату «N. Название»: «{h1.TextContent.Trim()}».");

        var number = int.Parse(heading.Groups[1].Value, CultureInfo.InvariantCulture);
        var blocks = element.Children.SkipWhile(c => c != h1).Skip(1).ToList();

        var section = Section.Create(number, h1.Id ?? "", heading.Groups[2].Value, Html(blocks.TakeWhile(b => !IsHeading(b))));
        QuestionGroup? group = null;

        for (var i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i];
            if (!IsHeading(block))
                continue;   // содержимое уже забрал заголовок, после которого оно идёт

            var content = Html(blocks.Skip(i + 1).TakeWhile(b => !IsHeading(b)));
            if (block.LocalName == "h2")
            {
                group = section.AddGroup(block.TextContent, content);
                continue;
            }

            // у разделов без подзаголовков вопросы лежат в группе без названия
            group ??= section.AddGroup(null, "");
            var (code, title, importance) = ParseQuestionHeading(block);
            group.AddQuestion(code, title, importance, content);
        }

        return section;
    }

    private static (QuestionCode Code, string Title, Importance Importance) ParseQuestionHeading(IElement h3)
    {
        var id = h3.Id ?? "";
        if (!id.StartsWith('q') || !QuestionCode.TryParse(id[1..], out var code))
            throw new DomainException($"У вопроса неверный id: «{id}».");

        // «#» — ссылка-якорь, не часть названия
        var text = string.Concat(h3.ChildNodes
            .Where(n => n is not IElement { ClassList: var classes } || !classes.Contains("anchor"))
            .Select(n => n.TextContent)).Trim();

        var prefix = code + " ";
        if (text.StartsWith(prefix, StringComparison.Ordinal))
            text = text[prefix.Length..];

        var mark = TrailingMark().Match(text);
        var importance = mark.Success ? ImportanceExtensions.FromIcon(mark.Groups[1].Value) : Importance.None;
        return (code, mark.Success ? text[..mark.Index].TrimEnd() : text, importance);
    }

    private static bool IsHeading(IElement element) =>
        element.LocalName == "h2" || (element.LocalName == "h3" && element.Id?.StartsWith('q') == true);

    private static string Html(IEnumerable<IElement> elements)
    {
        var sb = new StringBuilder();
        foreach (var element in elements)
            sb.Append(element.OuterHtml).Append('\n');
        return sb.ToString().TrimEnd('\n');
    }

    [GeneratedRegex(@"^(\d+)\.\s+(.+)$")]
    private static partial Regex SectionHeading();

    [GeneratedRegex(@"\s*(🔥|⭐|🎓)\s*$")]
    private static partial Regex TrailingMark();
}
