using JobTracker.Application.Applications;
using JobTracker.Application.Content;
using JobTracker.Application.Notes;
using JobTracker.Application.Study;

namespace JobTracker.Api.Endpoints;

internal static class SyncEndpoints
{
    public static IEndpointRouteBuilder MapSyncEndpoints(this IEndpointRouteBuilder app)
    {
        // Снимок всех данных пользователя для офлайн-режима: страница хранит его в браузере и работает без сервера.
        // Данных немного (сотни записей), поэтому всегда целиком — так в копии не остаётся удалённого на другом устройстве.
        // Версия текстов — чтобы страница перекачала их, только если они изменились.
        app.MapGet("/api/sync", async (JobApplicationService applications, StudyService study, NotesService notes,
            ContentService content, CancellationToken ct) => new
        {
            ContentVersion = await content.GetVersionAsync(ct),
            Applications = await applications.ListAsync(ct),
            Study = await study.GetStateAsync(ct),
            Ideas = await notes.ListIdeasAsync(ct),
            Highlights = await notes.ListHighlightsAsync(ct),
        }).WithTags("Sync");

        return app;
    }
}
