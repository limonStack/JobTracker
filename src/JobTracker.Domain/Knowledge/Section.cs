using JobTracker.Domain.Common;

namespace JobTracker.Domain.Knowledge;

/// <summary>
/// Раздел базы знаний («7. Архитектура, принципы и паттерны») — общий контент для всех пользователей,
/// поэтому не <see cref="IUserOwned"/>. Внутри — группы вопросов («Принципы», «Подвохи»).
/// Ключ — номер раздела: на него и на номер вопроса ссылаются данные пользователей.
/// </summary>
public sealed class Section
{
    private readonly List<QuestionGroup> _groups = [];

    public int Number { get; private set; }

    /// <summary>Якорь раздела в тексте: «07-architecture».</summary>
    public string Slug { get; private set; } = null!;

    public string Title { get; private set; } = null!;

    /// <summary>Текст до первой группы или вопроса; у шпаргалки — весь раздел.</summary>
    public string IntroHtml { get; private set; } = "";

    public IReadOnlyList<QuestionGroup> Groups => _groups;

    public IEnumerable<Question> Questions => _groups.SelectMany(g => g.Questions);

    private Section() { } // для EF Core

    public static Section Create(int number, string slug, string title, string introHtml)
    {
        if (number <= 0)
            throw new DomainException("Номер раздела должен быть положительным.");
        if (string.IsNullOrWhiteSpace(slug) || string.IsNullOrWhiteSpace(title))
            throw new DomainException($"У раздела {number} нет якоря или названия.");

        return new Section { Number = number, Slug = slug.Trim(), Title = title.Trim(), IntroHtml = introHtml };
    }

    /// <summary>Новая группа в конце раздела. Без названия — вопросы раздела, у которого нет подзаголовков.</summary>
    public QuestionGroup AddGroup(string? title, string introHtml)
    {
        var group = new QuestionGroup(Number, _groups.Count + 1, title, introHtml);
        _groups.Add(group);
        return group;
    }
}

/// <summary>Подзаголовок раздела («Подвохи») со вступлением и вопросами по порядку.</summary>
public sealed class QuestionGroup
{
    private readonly List<Question> _questions = [];

    public int SectionNumber { get; private set; }

    /// <summary>Порядковый номер группы в разделе, с 1.</summary>
    public int Order { get; private set; }

    public string? Title { get; private set; }

    public string IntroHtml { get; private set; } = "";

    public IReadOnlyList<Question> Questions => _questions;

    private QuestionGroup() { } // для EF Core

    internal QuestionGroup(int sectionNumber, int order, string? title, string introHtml)
    {
        SectionNumber = sectionNumber;
        Order = order;
        Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
        IntroHtml = introHtml;
    }

    public Question AddQuestion(QuestionCode code, string title, Importance importance, string bodyHtml)
    {
        if (code.Section != SectionNumber)
            throw new DomainException($"Вопрос {code} не из раздела {SectionNumber}.");

        var question = new Question(code, SectionNumber, Order, _questions.Count + 1, title, importance, bodyHtml);
        _questions.Add(question);
        return question;
    }
}

/// <summary>Вопрос с ответом. Подвох — задача «что выведет код» или вопрос с неверной предпосылкой.</summary>
public sealed class Question
{
    private const string TrickPrefix = "Подвох";

    /// <summary>«7.1» — ключ вопроса; на него ссылаются отметки, выделения и идеи пользователей.</summary>
    public QuestionCode Code { get; private set; }

    public int SectionNumber { get; private set; }

    public int GroupOrder { get; private set; }

    /// <summary>Порядок в группе, с 1: как в исходном тексте, даже если номера идут не подряд.</summary>
    public int Order { get; private set; }

    public string Title { get; private set; } = null!;

    public Importance Importance { get; private set; }

    public bool IsTrick => Title.StartsWith(TrickPrefix, StringComparison.Ordinal);

    public string BodyHtml { get; private set; } = "";

    private Question() { } // для EF Core

    internal Question(QuestionCode code, int sectionNumber, int groupOrder, int order, string title, Importance importance, string bodyHtml)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException($"У вопроса {code} нет названия.");

        Code = code;
        SectionNumber = sectionNumber;
        GroupOrder = groupOrder;
        Order = order;
        Title = title.Trim();
        Importance = importance;
        BodyHtml = bodyHtml;
    }
}
