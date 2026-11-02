using JobTracker.Application.Abstractions;
using JobTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JobTracker.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("JobTracker") ?? "Data Source=jobtracker.db";

        services.AddDbContext<JobTrackerDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<JobTrackerDbContext>());
        services.AddScoped<IJobApplicationRepository, JobApplicationRepository>();
        services.AddScoped<DomainEventDispatcher>();

        return services;
    }
}
