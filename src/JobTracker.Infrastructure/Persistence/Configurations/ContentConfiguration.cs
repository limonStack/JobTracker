using JobTracker.Domain.Knowledge;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobTracker.Infrastructure.Persistence.Configurations;

// Контент общий для всех пользователей: без UserId и без фильтра по владельцу.
// Ключи естественные (номер раздела, «7.1»): на них ссылаются данные пользователей, и они не меняются при переимпорте.

internal sealed class SectionConfiguration : IEntityTypeConfiguration<Section>
{
    public void Configure(EntityTypeBuilder<Section> builder)
    {
        builder.ToTable("Sections");
        builder.HasKey(x => x.Number);
        builder.Property(x => x.Number).ValueGeneratedNever();
        builder.Property(x => x.Slug).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(300).IsRequired();
        builder.Property(x => x.IntroHtml).IsRequired();
        builder.Ignore(x => x.Questions);

        builder.HasMany(x => x.Groups).WithOne()
            .HasForeignKey(g => g.SectionNumber)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class QuestionGroupConfiguration : IEntityTypeConfiguration<QuestionGroup>
{
    public void Configure(EntityTypeBuilder<QuestionGroup> builder)
    {
        builder.ToTable("QuestionGroups");
        builder.HasKey(x => new { x.SectionNumber, x.Order });
        builder.Property(x => x.Title).HasMaxLength(300);
        builder.Property(x => x.IntroHtml).IsRequired();

        builder.HasMany(x => x.Questions).WithOne()
            .HasForeignKey(q => new { q.SectionNumber, q.GroupOrder })
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.ToTable("Questions");
        builder.HasKey(x => x.Code);
        builder.Property(x => x.Code)
            .HasConversion(code => code.ToString(), value => QuestionCode.Parse(value))
            .HasMaxLength(10)
            .ValueGeneratedNever();
        builder.Property(x => x.Title).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Importance).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.BodyHtml).IsRequired();
        builder.Ignore(x => x.IsTrick);
    }
}

internal sealed class ContentVersionConfiguration : IEntityTypeConfiguration<ContentVersion>
{
    public void Configure(EntityTypeBuilder<ContentVersion> builder)
    {
        builder.ToTable("ContentVersions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Version).HasMaxLength(64).IsRequired();
    }
}
