using JobTracker.Application.Abstractions;
using JobTracker.Application.Content;
using JobTracker.Infrastructure.Content;
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
        services.AddScoped<IContentRepository, ContentRepository>();
        services.AddScoped<IStudyRepository, StudyRepository>();
        services.AddSingleton<IContentSource, FileContentSource>();

        services.AddSingleton<IVacancyCheckQueue, ChannelVacancyCheckQueue>();
        services.AddVacancyChecker<DouChecker>();
        services.AddVacancyChecker<WorkUaChecker>();

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
                client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
                // Work.ua (Cloudflare) без Sec-Fetch-* отвечает 403 даже с браузерным User-Agent.
                // Этого мало: HttpClient Cloudflare всё равно отсекает по TLS-отпечатку (curl с теми же заголовками проходит)
                client.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
                client.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
                client.DefaultRequestHeaders.Add("Sec-Fetch-Site", "none");
            })
            // Polly в цепочке DelegatingHandler-ов: повтор при сбое сети, 5xx, 408 и 429, таймаут на попытку и на всё.
            // 404 не повторяется — это ответ, а не сбой
            .AddStandardResilienceHandler(o =>
            {
                o.Retry.MaxRetryAttempts = 2;
                o.Retry.Delay = TimeSpan.FromSeconds(2);
                o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(15);
                o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(60);
                o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
            });
        services.AddTransient<IVacancyChecker>(sp => sp.GetRequiredService<TChecker>());
    }

    private const string BrowserUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36";
}
