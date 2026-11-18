using JobTracker.Application.Abstractions;
using JobTracker.Application.Applications;
using JobTracker.Application.Content;
using JobTracker.Application.Notes;
using JobTracker.Application.Study;
using JobTracker.Application.Import;
using JobTracker.Application.VacancyChecking;
using JobTracker.Domain.Applications;
using Microsoft.Extensions.DependencyInjection;

namespace JobTracker.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<JobApplicationService>();
        services.AddScoped<ContentService>();
        services.AddScoped<StudyService>();
        services.AddScoped<NotesService>();
        services.AddScoped<IUserClock, UserClock>();
        services.AddScoped<LegacyImportService>();
        services.AddScoped<LegacySiteImportService>();
        services.AddScoped<VacancyCheckerResolver>();
        services.AddScoped<VacancyCheckService>();
        services.AddSingleton<VacancyCheckProgress>();
        services.AddScoped<IDomainEventHandler<VacancyClosedEvent>, LogVacancyClosedHandler>();
        return services;
    }
}
