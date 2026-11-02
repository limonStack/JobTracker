using JobTracker.Application.Applications;
using JobTracker.Application.Import;

namespace JobTracker.Api.Endpoints;

internal static class ApplicationEndpoints
{
    public static IEndpointRouteBuilder MapApplicationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/applications").WithTags("Applications");

        group.MapGet("/", (JobApplicationService service, CancellationToken ct) => service.ListAsync(ct));

        group.MapGet("/{id:guid}", async (Guid id, JobApplicationService service, CancellationToken ct) =>
            OkOrNotFound(await service.GetAsync(id, ct)));

        group.MapPost("/", async (SaveApplicationRequest request, JobApplicationService service, CancellationToken ct) =>
        {
            var created = await service.CreateAsync(request, ct);
            return Results.Created($"/api/applications/{created.Id}", created);
        });

        group.MapPut("/{id:guid}", async (Guid id, SaveApplicationRequest request, JobApplicationService service,
            CancellationToken ct) => OkOrNotFound(await service.UpdateAsync(id, request, ct)));

        group.MapPut("/{id:guid}/stage", async (Guid id, ChangeStageRequest request, JobApplicationService service,
            CancellationToken ct) => OkOrNotFound(await service.ChangeStageAsync(id, request.Stage, ct)));

        group.MapPost("/{id:guid}/events", async (Guid id, AddEventRequest request, JobApplicationService service,
            CancellationToken ct) => OkOrNotFound(await service.AddEventAsync(id, request, ct)));

        group.MapDelete("/{id:guid}/events/{eventId:guid}", async (Guid id, Guid eventId, JobApplicationService service,
            CancellationToken ct) => OkOrNotFound(await service.RemoveEventAsync(id, eventId, ct)));

        group.MapDelete("/{id:guid}", async (Guid id, JobApplicationService service, CancellationToken ct) =>
            await service.DeleteAsync(id, ct) ? Results.NoContent() : Results.NotFound());

        app.MapPost("/api/import", (IReadOnlyList<LegacyJob> jobs, LegacyImportService service, CancellationToken ct) =>
            service.ImportAsync(jobs, ct)).WithTags("Import");

        app.MapGet("/api/export", async (JobApplicationService service, CancellationToken ct) =>
            Results.File(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(await service.ListAsync(ct), JsonDefaults.Options),
                "application/json", $"jobtracker-{DateTime.Now:yyyy-MM-dd}.json")).WithTags("Import");

        return app;
    }

    private static IResult OkOrNotFound<T>(T? value) where T : class =>
        value is null ? Results.NotFound() : Results.Ok(value);
}
