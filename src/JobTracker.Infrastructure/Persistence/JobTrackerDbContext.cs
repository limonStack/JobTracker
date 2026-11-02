using JobTracker.Application.Abstractions;
using JobTracker.Domain.Applications;
using JobTracker.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.Infrastructure.Persistence;

/// <summary>
/// DbContext — он же Unit of Work: одна операция = один SaveChanges.
/// После успешного сохранения рассылает доменные события, накопленные сущностями.
/// </summary>
public sealed class JobTrackerDbContext(DbContextOptions<JobTrackerDbContext> options, DomainEventDispatcher dispatcher)
    : DbContext(options), IUnitOfWork
{
    public DbSet<JobApplication> Applications => Set<JobApplication>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(JobTrackerDbContext).Assembly);

    async Task IUnitOfWork.SaveChangesAsync(CancellationToken ct)
    {
        var entities = ChangeTracker.Entries<Entity>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Count > 0)
            .ToList();
        var events = entities.SelectMany(e => e.DomainEvents).ToList();
        entities.ForEach(e => e.ClearDomainEvents());

        await SaveChangesAsync(ct);
        // события — после коммита: подписчики не должны реагировать на то, что не сохранилось
        await dispatcher.DispatchAsync(events, ct);
    }
}
