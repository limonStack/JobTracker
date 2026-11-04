using JobTracker.Application.Study;
using JobTracker.Domain.Study;

namespace JobTracker.Api.Endpoints;

internal static class StudyEndpoints
{
    public static IEndpointRouteBuilder MapStudyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/study").WithTags("Study");

        // всё состояние учёбы одним запросом: прогресс, календарь и «Повторить» считает интерфейс
        group.MapGet("/", (StudyService service, CancellationToken ct) => service.GetStateAsync(ct));

        // PUT/DELETE, а не «переключить»: повтор запроса после сбоя сети не снимет только что поставленную отметку
        group.MapPut("/marks/{code}", async (string code, MarkStudiedRequest? request, StudyService service, CancellationToken ct) =>
            await service.MarkStudiedAsync(code, request?.Day, ct) is { } mark ? Results.Ok(mark) : Results.NotFound());

        group.MapDelete("/marks/{code}", async (string code, StudyService service, CancellationToken ct) =>
        {
            await service.UnmarkStudiedAsync(code, ct);
            return Results.NoContent();
        });

        group.MapPut("/activities/{kind}/{day}", (string kind, DateOnly day, StudyService service, CancellationToken ct) =>
            SetActivity(kind, day, true, service, ct));
        group.MapDelete("/activities/{kind}/{day}", (string kind, DateOnly day, StudyService service, CancellationToken ct) =>
            SetActivity(kind, day, false, service, ct));

        group.MapPut("/reviews/{day}/{code}", (DateOnly day, string code, StudyService service, CancellationToken ct) =>
            SetReviewed(day, code, true, service, ct));
        group.MapDelete("/reviews/{day}/{code}", (DateOnly day, string code, StudyService service, CancellationToken ct) =>
            SetReviewed(day, code, false, service, ct));

        var settings = app.MapGroup("/api/settings").WithTags("Settings");
        settings.MapGet("/", (StudyService service, CancellationToken ct) => service.GetSettingsAsync(ct));
        settings.MapPut("/", (SettingsDto request, StudyService service, CancellationToken ct) => service.UpdateSettingsAsync(request, ct));

        return app;
    }

    private static async Task<IResult> SetActivity(string kind, DateOnly day, bool done, StudyService service, CancellationToken ct)
    {
        // в адресе — «workout» / «ai», регистр не важен; числа («0») не принимаем
        if (!Enum.TryParse<ActivityKind>(kind, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed) || char.IsDigit(kind[0]))
            return Results.NotFound();

        await service.SetActivityAsync(parsed, day, done, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> SetReviewed(DateOnly day, string code, bool reviewed, StudyService service, CancellationToken ct) =>
        await service.SetReviewedAsync(day, code, reviewed, ct) ? Results.NoContent() : Results.NotFound();
}
