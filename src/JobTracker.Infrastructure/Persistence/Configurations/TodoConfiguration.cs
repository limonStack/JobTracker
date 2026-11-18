using JobTracker.Domain.Todos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobTracker.Infrastructure.Persistence.Configurations;

internal sealed class TodoConfiguration : IEntityTypeConfiguration<TodoItem>
{
    public void Configure(EntityTypeBuilder<TodoItem> builder)
    {
        builder.ToTable("Todos");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Ignore(x => x.DomainEvents);
        builder.Ignore(x => x.IsDone);
        builder.Property(x => x.Title).HasMaxLength(TodoItem.TitleMaxLength).IsRequired();
        builder.Property<Guid>(UserOwnership.Property);
        builder.HasIndex(UserOwnership.Property, nameof(TodoItem.PlannedOn));
    }
}
