using System.Net;
using System.Net.Http.Headers;
using JobTracker.Application.Content;
using JobTracker.Domain.Knowledge;
using Microsoft.Extensions.DependencyInjection;

namespace JobTracker.IntegrationTests;

/// <summary>Тексты ответов: импорт из content/answers.html при старте и выдача через API.</summary>
public sealed class ContentApiTests : IDisposable
{
    private readonly JobTrackerApiFactory _factory = new() { ImportContent = true };
    private readonly HttpClient _client;

    public ContentApiTests() => _client = _factory.CreateClient();

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task GetContent_ReturnsImportedSectionsAndQuestions()
    {
        var content = await (await _client.GetAsync("/api/content")).ReadAsync<ContentDto>();

        Assert.Contains(content.Sections, s => s.Number == 1);
        var q = content.Sections.Single(s => s.Number == 7).Groups.SelectMany(g => g.Questions).First();
        Assert.Equal(("7.1", "SOLID", Importance.Must), (q.Code, q.Title, q.Importance));
        Assert.Contains("<", q.BodyHtml, StringComparison.Ordinal);
        Assert.False(string.IsNullOrEmpty(content.Version));
    }

    [Fact]
    public async Task GetContent_SameVersion_Returns304()
    {
        var first = await _client.GetAsync("/api/content");
        var etag = first.Headers.ETag;
        Assert.NotNull(etag);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/content");
        request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(etag.Tag));
        var second = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
    }

    [Fact]
    public async Task ImportAgain_SameFiles_DoesNothing()
    {
        await using var scope = _factory.Services.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<ContentService>().ImportIfChangedAsync(default);

        Assert.False(result.Imported);
        Assert.True(result.Questions >= 328);
    }
}
