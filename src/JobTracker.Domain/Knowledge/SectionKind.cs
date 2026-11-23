namespace JobTracker.Domain.Knowledge;

/// <summary>Вид раздела: общая база вопросов или подготовка к конкретной компании.</summary>
public enum SectionKind
{
    /// <summary>Общая база — входит в общий список и общий прогресс.</summary>
    General,

    /// <summary>Раздел под вакансию компании (skill interview-prep) — в меню отдельным блоком, в общий прогресс не входит.</summary>
    Company,
}
