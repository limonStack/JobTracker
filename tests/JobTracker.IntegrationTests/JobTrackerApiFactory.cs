using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using JobTracker.Application.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace JobTracker.IntegrationTests;

/// <summary>Поднимает Api в памяти с отдельной временной базой SQLite — тесты не трогают рабочую jobtracker.db.</summary>
public sealed class JobTrackerApiFactory : WebApplicationFactory<Program>
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"jobtracker-test-{Guid.NewGuid():N}.db");

    /// <summary>Вместо настоящего DOU: тесты не ходят в сеть и сами задают, какая вакансия закрыта.</summary>
    public FakeVacancyChecker Checker { get; } = new();

    /// <summary>Импортировать тексты ответов при старте. По умолчанию нет: разбор 2 МБ HTML не нужен тестам откликов.</summary>
    public bool ImportContent { get; init; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:JobTracker", $"Data Source={_dbPath}");
        builder.UseSetting("Content:ImportOnStartup", ImportContent ? "true" : "false");
        builder.UseSetting("VacancyChecks:CheckOnStartup", "false");
        builder.UseSetting("VacancyChecks:DelayBetweenRequests", "00:00:00");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IVacancyChecker>();
            services.AddSingleton<IVacancyChecker>(Checker);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools(); // иначе пул держит файл открытым и его не удалить
        File.Delete(_dbPath);
    }
}

internal static class HttpExtensions
{
    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(JobTrackerApiFactory.Json))!;
    }

    public static Task<HttpResponseMessage> PostJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PostAsJsonAsync(url, body, JobTrackerApiFactory.Json);

    public static Task<HttpResponseMessage> PutJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PutAsJsonAsync(url, body, JobTrackerApiFactory.Json);
}
