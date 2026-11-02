using JobTracker.Domain.Applications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace JobTracker.Infrastructure.Persistence.Configurations;

internal sealed class JobApplicationConfiguration : IEntityTypeConfiguration<JobApplication>
{
    public void Configure(EntityTypeBuilder<JobApplication> builder)
    {
        builder.ToTable("Applications");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Ignore(x => x.DomainEvents);
        builder.Ignore(x => x.IsOpen);

        builder.Property(x => x.Company).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(2000);
        builder.Property(x => x.Url)
            .HasConversion(url => url!.Value, value => VacancyUrl.Create(value)!)
            .HasMaxLength(500);

        // enum-ы строками: базу можно читать глазами, а перестановка значений в enum ничего не ломает
        builder.Property(x => x.Stage).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.ReachedStage).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.VacancyStatus).HasConversion<string>().HasMaxLength(20);

        builder.Property(x => x.LegacyId).HasMaxLength(50);
        builder.HasIndex(x => x.LegacyId).IsUnique();

        builder.HasMany(x => x.Events).WithOne().HasForeignKey("ApplicationId").OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Checks).WithOne().HasForeignKey("ApplicationId").OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ApplicationEventConfiguration : IEntityTypeConfiguration<ApplicationEvent>
{
    public void Configure(EntityTypeBuilder<ApplicationEvent> builder)
    {
        builder.ToTable("ApplicationEvents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Ignore(x => x.DomainEvents);
        builder.Property(x => x.Text).HasMaxLength(500).IsRequired();
    }
}

internal sealed class VacancyCheckConfiguration : IEntityTypeConfiguration<VacancyCheck>
{
    public void Configure(EntityTypeBuilder<VacancyCheck> builder)
    {
        builder.ToTable("VacancyChecks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Ignore(x => x.DomainEvents);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.Error).HasMaxLength(500);
    }
}
