using JobTracker.Domain.Knowledge;
using JobTracker.Domain.Study;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobTracker.Infrastructure.Persistence.Configurations;

// Учёба пользователя. На вопрос ссылаемся номером «7.1», без внешнего ключа на Questions:
// тексты переимпортируются целиком, и отметка не должна пропадать, если вопрос временно исчез из файла.
// Уникальность — всегда вместе с UserId: у разных пользователей одинаковые отметки.

internal static class QuestionCodeConversion
{
    public static PropertyBuilder<QuestionCode> AsQuestionCode(this PropertyBuilder<QuestionCode> property) =>
        property.HasConversion(code => code.ToString(), value => QuestionCode.Parse(value)).HasMaxLength(10);
}

internal sealed class StudyMarkConfiguration : IEntityTypeConfiguration<StudyMark>
{
    public void Configure(EntityTypeBuilder<StudyMark> builder)
    {
        builder.ToTable("StudyMarks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Ignore(x => x.DomainEvents);
        builder.Property(x => x.Code).AsQuestionCode();
        builder.Property<Guid>(UserOwnership.Property);
        builder.HasIndex(UserOwnership.Property, nameof(StudyMark.Code)).IsUnique();
    }
}

internal sealed class ActivityDayConfiguration : IEntityTypeConfiguration<ActivityDay>
{
    public void Configure(EntityTypeBuilder<ActivityDay> builder)
    {
        builder.ToTable("ActivityDays");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Ignore(x => x.DomainEvents);
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property<Guid>(UserOwnership.Property);
        builder.HasIndex(UserOwnership.Property, nameof(ActivityDay.Kind), nameof(ActivityDay.Day)).IsUnique();
    }
}

internal sealed class ReviewMarkConfiguration : IEntityTypeConfiguration<ReviewMark>
{
    public void Configure(EntityTypeBuilder<ReviewMark> builder)
    {
        builder.ToTable("ReviewMarks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Ignore(x => x.DomainEvents);
        builder.Property(x => x.Code).AsQuestionCode();
        builder.Property<Guid>(UserOwnership.Property);
        builder.HasIndex(UserOwnership.Property, nameof(ReviewMark.Day), nameof(ReviewMark.Code)).IsUnique();
    }
}

internal sealed class UserSettingsConfiguration : IEntityTypeConfiguration<UserSettings>
{
    public void Configure(EntityTypeBuilder<UserSettings> builder)
    {
        builder.ToTable("UserSettings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Ignore(x => x.DomainEvents);
        builder.Property(x => x.TimeZone).HasMaxLength(100);
        // одна запись настроек на пользователя
        builder.Property<Guid>(UserOwnership.Property);
        builder.HasIndex(UserOwnership.Property).IsUnique();
    }
}
