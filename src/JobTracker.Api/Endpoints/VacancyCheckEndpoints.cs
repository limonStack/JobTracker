using JobTracker.Application.Abstractions;
using JobTracker.Application.VacancyChecking;

namespace JobTracker.Api.Endpoints;

internal static class VacancyCheckEndpoints
{
    public static IEndpointRouteBuilder MapVacancyCheckEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api").WithTags("Vacancy checks");

        // одна вакансия — сразу, ответ уже с новым статусом
        group.MapPost("/applications/{id:guid}/check", async (Guid id, VacancyCheckService service, CancellationToken ct) =>
            await service.CheckOneAsync(id, ct) is { } dto ? Results.Ok(dto) : Results.NotFound());

        // все — в фоне: 202, а ход проверки — через GET /api/checks
        group.MapPost("/checks", (IVacancyCheckQueue queue, VacancyCheckProgress progress) =>
        {
            queue.RequestCheckAll();
            return Results.Accepted("/api/checks", Status(queue, progress));
        });

        group.MapGet("/checks", (IVacancyCheckQueue queue, VacancyCheckProgress progress) => Status(queue, progress));

        return app;
    }

    // «идёт» — и когда обход выполняется, и когда он ещё ждёт в очереди: иначе интерфейс решит, что всё закончилось
    private static VacancyCheckStatus Status(IVacancyCheckQueue queue, VacancyCheckProgress progress) =>
        progress.Status with { Running = progress.Status.Running || queue.HasPending };
}
