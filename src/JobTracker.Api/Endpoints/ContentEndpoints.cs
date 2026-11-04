using JobTracker.Application.Content;
using Microsoft.Net.Http.Headers;

namespace JobTracker.Api.Endpoints;

internal static class ContentEndpoints
{
    public static IEndpointRouteBuilder MapContentEndpoints(this IEndpointRouteBuilder app)
    {
        // все разделы с ответами одним ответом (поиску нужен весь текст). Тексты меняются редко:
        // ETag = версия контента, браузер переспрашивает с If-None-Match и получает 304 без тела
        app.MapGet("/api/content", async (HttpContext http, ContentService content, CancellationToken ct) =>
        {
            if (await content.GetVersionAsync(ct) is not { } version)
                return Results.NotFound();

            var etag = $"\"{version}\"";
            http.Response.Headers.ETag = etag;
            http.Response.Headers.CacheControl = "no-cache";
            if (http.Request.Headers.IfNoneMatch.Contains(etag))
                return Results.StatusCode(StatusCodes.Status304NotModified);

            return await content.GetAsync(ct) is { } dto ? Results.Ok(dto) : Results.NotFound();
        }).WithTags("Content");

        return app;
    }
}
