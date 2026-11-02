using JobTracker.Api;
using JobTracker.Api.BackgroundJobs;
using JobTracker.Api.Endpoints;
using JobTracker.Application;
using JobTracker.Application.VacancyChecking;
using JobTracker.Infrastructure;
using JobTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services.AddOptions<VacancyCheckOptions>()
    .BindConfiguration(VacancyCheckOptions.Section)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddHostedService<VacancyCheckWorker>();

builder.Services.ConfigureHttpJsonOptions(o => JsonDefaults.Configure(o.SerializerOptions));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

var app = builder.Build();

// приложение локальное и однопользовательское — схему базы обновляем при старте
await using (var scope = app.Services.CreateAsyncScope())
    await scope.ServiceProvider.GetRequiredService<JobTrackerDbContext>().Database.MigrateAsync();

app.UseExceptionHandler();
app.UseStatusCodePages();

// фронтенд — статические файлы из wwwroot с того же адреса, поэтому запросы к API не упираются в CORS
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapApplicationEndpoints();
app.MapVacancyCheckEndpoints();

await app.RunAsync();

public partial class Program; // для WebApplicationFactory в интеграционных тестах
