using System.Text;
using JobTracker.Application.Notes;

namespace JobTracker.Api.Endpoints;

internal static class NotesEndpoints
{
    public static IEndpointRouteBuilder MapNotesEndpoints(this IEndpointRouteBuilder app)
    {
        var ideas = app.MapGroup("/api/ideas").WithTags("Ideas");

        ideas.MapGet("/", (NotesService service, CancellationToken ct) => service.ListIdeasAsync(ct));

        ideas.MapPost("/", async (CreateIdeaRequest request, NotesService service, CancellationToken ct) =>
        {
            var created = await service.CreateIdeaAsync(request, ct);
            return Results.Created($"/api/ideas/{created.Id}", created);
        });

        ideas.MapPut("/{id:guid}", async (Guid id, UpdateIdeaRequest request, NotesService service, CancellationToken ct) =>
            OkOrNotFound(await service.UpdateIdeaAsync(id, request, ct)));

        ideas.MapPut("/{id:guid}/status", async (Guid id, ChangeIdeaStatusRequest request, NotesService service, CancellationToken ct) =>
            OkOrNotFound(await service.ChangeIdeaStatusAsync(id, request.Status, ct)));

        ideas.MapDelete("/{id:guid}", async (Guid id, NotesService service, CancellationToken ct) =>
            await service.DeleteIdeaAsync(id, ct) ? Results.NoContent() : Results.NotFound());

        ideas.MapGet("/export.md", async (NotesService service, CancellationToken ct) =>
            Results.File(Encoding.UTF8.GetBytes(await service.ExportIdeasMarkdownAsync(ct)),
                "text/markdown; charset=utf-8", $"идеи-{DateTime.Now:yyyy-MM-dd}.md"));

        var highlights = app.MapGroup("/api/highlights").WithTags("Highlights");

        highlights.MapGet("/", (NotesService service, CancellationToken ct) => service.ListHighlightsAsync(ct));

        highlights.MapPost("/", async (SaveHighlightRequest request, NotesService service, CancellationToken ct) =>
        {
            var created = await service.CreateHighlightAsync(request, ct);
            return Results.Created($"/api/highlights/{created.Id}", created);
        });

        highlights.MapPut("/{id:guid}", async (Guid id, SaveHighlightRequest request, NotesService service, CancellationToken ct) =>
            OkOrNotFound(await service.UpdateHighlightAsync(id, request, ct)));

        highlights.MapDelete("/{id:guid}", async (Guid id, NotesService service, CancellationToken ct) =>
            await service.DeleteHighlightAsync(id, ct) ? Results.NoContent() : Results.NotFound());

        return app;
    }

    private static IResult OkOrNotFound<T>(T? value) where T : class =>
        value is null ? Results.NotFound() : Results.Ok(value);
}
