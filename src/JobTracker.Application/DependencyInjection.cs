using JobTracker.Application.Applications;
using JobTracker.Application.Import;
using Microsoft.Extensions.DependencyInjection;

namespace JobTracker.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<JobApplicationService>();
        services.AddScoped<LegacyImportService>();
        return services;
    }
}
