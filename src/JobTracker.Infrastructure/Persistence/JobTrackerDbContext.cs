using System.Reflection;
using JobTracker.Application.Abstractions;
using JobTracker.Domain.Applications;
using JobTracker.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.Infrastructure.Persistence;

/// <summary>Имена, по которым слой хранения узнаёт владельца записи.</summary>
internal static class UserOwnership
{
    /// <summary>Теневое свойство и колонка с id владельца у каждой <see cref="IUserOwned"/>-сущности.</summary>
    public const string Property = "UserId";

    /// <summary>Именованный глобальный фильтр: <c>IgnoreQueryFilters([Filter])</c> снимает только его.</summary>
    public const string Filter = "ByUser";
}

/// <summary>
/// DbContext — он же Unit of Work: одна операция = один SaveChanges.
/// После успешного сохранения рассылает доменные события, накопленные сущностями.
/// Данные пользователей разделены здесь, а не в каждом запросе: у <see cref="IUserOwned"/>-сущностей
/// есть колонка UserId, запросы видят только записи текущего пользователя, новые записи получают его id сами.
/// </summary>
public sealed class JobTrackerDbContext(
    DbContextOptions<JobTrackerDbContext> options,
    DomainEventDispatcher dispatcher,
    ICurrentUser currentUser)
    : DbContext(options), IUnitOfWork
{
    private static readonly MethodInfo ApplyOwnershipMethod =
        typeof(JobTrackerDbContext).GetMethod(nameof(ApplyOwnership), BindingFlags.NonPublic | BindingFlags.Instance)!;

    public DbSet<JobApplication> Applications => Set<JobApplication>();

    // свойство контекста, а не захваченное значение: EF подставляет его параметром в каждый запрос этого экземпляра
    private Guid CurrentUserId => currentUser.UserId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(JobTrackerDbContext).Assembly);

        var owned = modelBuilder.Model.GetEntityTypes()
            .Where(t => t.BaseType is null && typeof(IUserOwned).IsAssignableFrom(t.ClrType))
            .Select(t => t.ClrType)
            .ToList();
        foreach (var type in owned)
            ApplyOwnershipMethod.MakeGenericMethod(type).Invoke(this, [modelBuilder]);
    }

    private void ApplyOwnership<T>(ModelBuilder modelBuilder) where T : class, IUserOwned
    {
        var entity = modelBuilder.Entity<T>();
        entity.Property<Guid>(UserOwnership.Property).IsRequired();
        entity.HasIndex(UserOwnership.Property);
        entity.HasQueryFilter(UserOwnership.Filter, x => EF.Property<Guid>(x, UserOwnership.Property) == CurrentUserId);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampOwner();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        StampOwner();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>Новая запись принадлежит текущему пользователю; без пользователя сохранять нельзя.</summary>
    private void StampOwner()
    {
        foreach (var entry in ChangeTracker.Entries<IUserOwned>().Where(e => e.State == EntityState.Added))
        {
            var owner = entry.Property<Guid>(UserOwnership.Property);
            if (owner.CurrentValue != Guid.Empty)
                continue;
            if (currentUser.UserId == Guid.Empty)
                throw new InvalidOperationException("Нельзя сохранить данные пользователя без текущего пользователя.");
            owner.CurrentValue = currentUser.UserId;
        }
    }

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
