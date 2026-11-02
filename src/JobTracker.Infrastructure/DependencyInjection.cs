using JobTracker.Application.Abstractions;
using JobTracker.Infrastructure.Persistence;
using JobTracker.Infrastructure.VacancyChecking;
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

        services.AddVacancyChecker<DouChecker>();

        return services;
    }

    /// <summary>Typed HttpClient для сайта + регистрация как IVacancyChecker: так резолвер увидит его в IEnumerable.</summary>
    private static void AddVacancyChecker<TChecker>(this IServiceCollection services)
        where TChecker : class, IVacancyChecker
    {
        services.AddHttpClient<TChecker>(client =>
        {
            // без браузерного User-Agent сайты за Cloudflare чаще отвечают 403
            client.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
            client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("uk,ru;q=0.9,en;q=0.8");
            client.Timeout = TimeSpan.FromSeconds(20);
        });
        services.AddTransient<IVacancyChecker>(sp => sp.GetRequiredService<TChecker>());
    }

    private const string BrowserUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36";
}
