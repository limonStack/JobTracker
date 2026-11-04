using JobTracker.Api;
using JobTracker.Api.BackgroundJobs;
using JobTracker.Api.Endpoints;
using JobTracker.Api.Users;
using JobTracker.Application.Abstractions;
using JobTracker.Application.Content;
using Microsoft.Extensions.Options;
using JobTracker.Application;
using JobTracker.Application.VacancyChecking;
using JobTracker.Infrastructure;
using JobTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<ICurrentUser, LocalCurrentUser>();

builder.Services.AddOptions<VacancyCheckOptions>()
    .BindConfiguration(VacancyCheckOptions.Section)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddHostedService<VacancyCheckWorker>();
builder.Services.AddOptions<ContentOptions>()
    .BindConfiguration(ContentOptions.Section)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.ConfigureHttpJsonOptions(o => JsonDefaults.Configure(o.SerializerOptions));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

var app = builder.Build();

// приложение локальное и однопользовательское — схему базы обновляем при старте
// тексты ответов — тоже при старте, но только если файлы изменились (сравнивается хеш)
await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<JobTrackerDbContext>().Database.MigrateAsync();
    if (scope.ServiceProvider.GetRequiredService<IOptions<ContentOptions>>().Value.ImportOnStartup)
        await scope.ServiceProvider.GetRequiredService<ContentService>().ImportIfChangedAsync(app.Lifetime.ApplicationStopping);
}

app.UseExceptionHandler();
app.UseStatusCodePages();

// фронтенд — статические файлы из wwwroot с того же адреса, поэтому запросы к API не упираются в CORS
app.UseDefaultFiles();
// no-cache: браузер каждый раз сверяет ETag (ответ 304, если файл не менялся) и не показывает старый JS/CSS после правок
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "no-cache",
});

app.MapApplicationEndpoints();
app.MapVacancyCheckEndpoints();
app.MapContentEndpoints();

await app.RunAsync();

public partial class Program; // для WebApplicationFactory в интеграционных тестах
