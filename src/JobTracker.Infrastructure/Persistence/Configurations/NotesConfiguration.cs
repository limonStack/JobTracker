using JobTracker.Domain.Notes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobTracker.Infrastructure.Persistence.Configurations;

internal sealed class IdeaConfiguration : IEntityTypeConfiguration<Idea>
{
    public void Configure(EntityTypeBuilder<Idea> builder)
    {
        builder.ToTable("Ideas");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Ignore(x => x.DomainEvents);
        builder.Property(x => x.Title).HasMaxLength(Idea.TitleMaxLength).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(Idea.NoteMaxLength);
        builder.Property(x => x.Project).HasMaxLength(200);
        builder.Property(x => x.Question).HasMaxLength(50);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.LegacyId).HasMaxLength(50);
        builder.Property<Guid>(UserOwnership.Property);
        builder.HasIndex(UserOwnership.Property, nameof(Idea.LegacyId)).IsUnique();
    }
}

internal sealed class HighlightConfiguration : IEntityTypeConfiguration<Highlight>
{
    public void Configure(EntityTypeBuilder<Highlight> builder)
    {
        builder.ToTable("Highlights");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Ignore(x => x.DomainEvents);
        builder.Property(x => x.Section).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Text).HasMaxLength(Highlight.TextMaxLength).IsRequired();
        builder.Property(x => x.Prefix).HasMaxLength(Highlight.ContextMaxLength);
        builder.Property(x => x.Suffix).HasMaxLength(Highlight.ContextMaxLength);
        builder.Property(x => x.Color).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.LegacyId).HasMaxLength(50);
        builder.Property<Guid>(UserOwnership.Property);
        builder.HasIndex(UserOwnership.Property, nameof(Highlight.LegacyId)).IsUnique();
    }
}
