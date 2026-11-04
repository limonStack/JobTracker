using JobTracker.Application.Abstractions;
using JobTracker.Domain.Applications;
using JobTracker.Infrastructure;
using JobTracker.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JobTracker.UnitTests.Persistence;

/// <summary>Данные разных пользователей не смешиваются — на настоящей SQLite, через тот же DI, что и в приложении.</summary>
public sealed class UserOwnershipTests : IDisposable
{
    private static readonly Guid Alice = Guid.CreateVersion7(), Bob = Guid.CreateVersion7();

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"jobtracker-owner-{Guid.NewGuid():N}.db");
    private readonly ServiceProvider _services;

    public UserOwnershipTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:JobTracker"] = $"Data Source={_dbPath}" })
            .Build();
        _services = new ServiceCollection()
            .AddLogging()
            .AddInfrastructure(config)
            .AddScoped<TestUser>()
            .AddScoped<ICurrentUser>(sp => sp.GetRequiredService<TestUser>())
            .BuildServiceProvider();

        using var scope = AsUser(Alice);
        scope.ServiceProvider.GetRequiredService<JobTrackerDbContext>().Database.Migrate();
    }

    public void Dispose()
    {
        _services.Dispose();
        SqliteConnection.ClearAllPools();
        File.Delete(_dbPath);
    }

    private IServiceScope AsUser(Guid id)
    {
        var scope = _services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TestUser>().UserId = id;
        return scope;
    }

    private async Task AddAsync(Guid user, string company, string? legacyId = null)
    {
        using var scope = AsUser(user);
        scope.ServiceProvider.GetRequiredService<IJobApplicationRepository>()
            .Add(JobApplication.Create(company, new DateOnly(2026, 11, 1), "https://jobs.dou.ua/companies/x/vacancies/1/", legacyId: legacyId));
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(default);
    }

    private async Task<List<string>> ListAsync(Guid user)
    {
        using var scope = AsUser(user);
        return (await scope.ServiceProvider.GetRequiredService<IJobApplicationRepository>().ListAsync()).Select(a => a.Company).ToList();
    }

    [Fact]
    public async Task EachUser_SeesOnlyOwnRecords()
    {
        await AddAsync(Alice, "Alice Co");
        await AddAsync(Bob, "Bob Co");

        Assert.Equal(["Alice Co"], await ListAsync(Alice));
        Assert.Equal(["Bob Co"], await ListAsync(Bob));
    }

    [Fact]
    public async Task Get_OtherUsersRecord_ReturnsNull()
    {
        await AddAsync(Alice, "Alice Co");
        Guid id;
        using (var alice = AsUser(Alice))
            id = (await alice.ServiceProvider.GetRequiredService<IJobApplicationRepository>().ListAsync()).Single().Id;

        using var bob = AsUser(Bob);
        Assert.Null(await bob.ServiceProvider.GetRequiredService<IJobApplicationRepository>().GetAsync(id));
    }

    [Fact]
    public async Task SameLegacyId_AllowedForDifferentUsers()
    {
        await AddAsync(Alice, "Alice Co", legacyId: "abc");
        await AddAsync(Bob, "Bob Co", legacyId: "abc");

        Assert.Single(await ListAsync(Bob));
    }

    [Fact]
    public async Task VacancyCheck_SeesAllUsers()
    {
        await AddAsync(Alice, "Alice Co");
        await AddAsync(Bob, "Bob Co");

        using var scope = AsUser(Alice);
        var all = await scope.ServiceProvider.GetRequiredService<IJobApplicationRepository>().ListForVacancyCheckAsync();

        Assert.Equal(2, all.Count);
    }

    /// <summary>Страховка на будущее: новая пользовательская сущность без UserId и фильтра не пройдёт этот тест.</summary>
    [Fact]
    public void EveryUserOwnedEntity_HasUserIdAndFilter()
    {
        using var scope = AsUser(Alice);
        var model = scope.ServiceProvider.GetRequiredService<JobTrackerDbContext>().Model;
        var owned = typeof(JobApplication).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(JobTracker.Domain.Common.IUserOwned).IsAssignableFrom(t))
            .ToList();

        Assert.NotEmpty(owned);
        foreach (var type in owned)
        {
            var entity = model.FindEntityType(type);
            Assert.True(entity is not null, $"{type.Name} не в модели EF");
            Assert.True(entity.FindProperty("UserId") is not null, $"у {type.Name} нет UserId");
            Assert.True(entity.GetDeclaredQueryFilters().Count > 0, $"у {type.Name} нет фильтра по пользователю");
        }
    }

    [Fact]
    public async Task Save_WithoutUser_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => AddAsync(Guid.Empty, "Nobody"));
    }

    private sealed class TestUser : ICurrentUser
    {
        public Guid UserId { get; set; }
    }
}
