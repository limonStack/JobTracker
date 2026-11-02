using JobTracker.Infrastructure;
using JobTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// приложение локальное и однопользовательское — схему базы обновляем при старте
await using (var scope = app.Services.CreateAsyncScope())
    await scope.ServiceProvider.GetRequiredService<JobTrackerDbContext>().Database.MigrateAsync();

app.MapGet("/", () => "JobTracker");

await app.RunAsync();

public partial class Program; // для WebApplicationFactory в интеграционных тестах
