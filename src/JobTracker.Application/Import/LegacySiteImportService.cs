using System.Globalization;
using System.Text.Json;
using JobTracker.Application.Abstractions;
using JobTracker.Application.Content;
using JobTracker.Domain.Common;
using JobTracker.Domain.Knowledge;
using JobTracker.Domain.Notes;
using JobTracker.Domain.Study;

namespace JobTracker.Application.Import;

/// <summary>Итог переноса по одному модулю: перенесено, уже было (или не нужно), ошибки.</summary>
public sealed record ImportPart(int Imported, int Skipped, IReadOnlyList<string> Errors);

/// <summary>Что перенеслось из старого сайта, по модулям. Отсутствующий в выгрузке модуль — null.</summary>
public sealed record LegacySiteImportResult(
    ImportPart? Progress,
    ImportPart? Workouts,
    ImportPart? Ai,
    ImportPart? Reviews,
    ImportPart? Jobs,
    ImportPart? Ideas,
    ImportPart? Highlights,
    bool SettingsApplied,
    IReadOnlyList<string> UnknownKeys);

/// <summary>
/// Перенос всех данных старого сайта (300questions/ANSWERS.html) из его localStorage.
/// На вход — ключи «net-interview-*» и их значения как есть (строки JSON). Старые данные только читаются.
/// Идемпотентен: что уже перенесено, пропускается, поэтому импорт можно повторять. Битая запись
/// не валит весь перенос — она попадает в Errors своего модуля.
/// </summary>
public sealed class LegacySiteImportService(
    IStudyRepository study,
    INotesRepository notes,
    IContentRepository content,
    LegacyImportService jobs,
    IUserClock clock,
    IUnitOfWork unitOfWork)
{
    public const string Prefix = "net-interview-";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly HashSet<string> KnownKeys =
    [
        "net-interview-progress-v1", "net-interview-workouts-v1", "net-interview-ai-v1", "net-interview-review-v1",
        "net-interview-jobs-v1", "net-interview-ideas-v1", "net-interview-highlights-v1",
        "net-interview-calendar-open", "net-interview-focus",
    ];

    public async Task<LegacySiteImportResult> ImportAsync(IReadOnlyDictionary<string, string?> storage, CancellationToken ct)
    {
        var today = await clock.TodayAsync(ct);
        var codes = await content.ListCodesAsync(ct);
        string? Get(string key) => storage.TryGetValue(Prefix + key, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;

        var result = new LegacySiteImportResult(
            Progress: Get("progress-v1") is { } p ? await ImportProgressAsync(p, codes, today, ct) : null,
            Workouts: Get("workouts-v1") is { } w ? await ImportActivityAsync(w, ActivityKind.Workout, today, ct) : null,
            Ai: Get("ai-v1") is { } a ? await ImportActivityAsync(a, ActivityKind.Ai, today, ct) : null,
            Reviews: Get("review-v1") is { } r ? await ImportReviewsAsync(r, codes, today, ct) : null,
            Jobs: null,
            Ideas: Get("ideas-v1") is { } i ? await ImportIdeasAsync(i, today, ct) : null,
            Highlights: Get("highlights-v1") is { } h ? await ImportHighlightsAsync(h, ct) : null,
            SettingsApplied: await ImportSettingsAsync(Get("calendar-open"), Get("focus"), ct),
            UnknownKeys: [.. storage.Keys.Where(k => !KnownKeys.Contains(k)).Order(StringComparer.Ordinal)]);

        await unitOfWork.SaveChangesAsync(ct);

        // отклики — своим сервисом, он сохраняет сам
        if (Get("jobs-v1") is { } j)
            result = result with { Jobs = await ImportJobsAsync(j, ct) };

        return result;
    }

    private sealed record LegacyProgress(string[]? Done, Dictionary<string, string>? DoneAt);

    private async Task<ImportPart> ImportProgressAsync(string raw, HashSet<QuestionCode> codes, DateOnly today, CancellationToken ct)
    {
        var errors = new List<string>();
        if (Parse<LegacyProgress>(raw, "прогресс", errors) is not { } progress)
            return new ImportPart(0, 0, errors);

        var existing = (await study.ListMarksAsync(ct)).Select(m => m.Code).ToHashSet();
        int imported = 0, skipped = 0;
        foreach (var raw1 in progress.Done ?? [])
        {
            // вопрос, которого больше нет в базе знаний, старый сайт тоже не показывал
            if (!QuestionCode.TryParse(raw1, out var code) || !codes.Contains(code))
            {
                errors.Add($"вопроса {raw1} нет в базе знаний");
                continue;
            }
            if (!existing.Add(code))
            {
                skipped++;
                continue;
            }

            var day = progress.DoneAt?.GetValueOrDefault(raw1) is { } d && TryDay(d, out var parsed) ? parsed : (DateOnly?)null;
            study.Add(StudyMark.Restore(code, day, today));
            imported++;
        }

        return new ImportPart(imported, skipped, errors);
    }

    private sealed record LegacyDays(Dictionary<string, bool>? Days);

    private async Task<ImportPart> ImportActivityAsync(string raw, ActivityKind kind, DateOnly today, CancellationToken ct)
    {
        var errors = new List<string>();
        if (Parse<LegacyDays>(raw, kind == ActivityKind.Workout ? "тренировки" : "ИИ", errors) is not { } data)
            return new ImportPart(0, 0, errors);

        var existing = (await study.ListActivitiesAsync(ct)).Where(a => a.Kind == kind).Select(a => a.Day).ToHashSet();
        int imported = 0, skipped = 0;
        foreach (var (key, on) in data.Days ?? [])
        {
            if (!on)
                continue;
            if (!TryDay(key, out var day) || day > today)
            {
                errors.Add($"неверный день {key}");
                continue;
            }
            if (!existing.Add(day))
            {
                skipped++;
                continue;
            }

            study.Add(ActivityDay.Create(kind, day, today));
            imported++;
        }

        return new ImportPart(imported, skipped, errors);
    }

    private async Task<ImportPart> ImportReviewsAsync(string raw, HashSet<QuestionCode> codes, DateOnly today, CancellationToken ct)
    {
        var errors = new List<string>();
        if (Parse<Dictionary<string, string[]>>(raw, "повторение", errors) is not { } data)
            return new ImportPart(0, 0, errors);

        var existing = (await study.ListReviewsAsync(ct)).Select(r => (r.Day, r.Code)).ToHashSet();
        var cutoff = ReviewMark.Cutoff(today);
        int imported = 0, skipped = 0;
        foreach (var (key, list) in data)
        {
            if (!TryDay(key, out var day) || day > today)
            {
                errors.Add($"неверный день {key}");
                continue;
            }
            foreach (var raw1 in list ?? [])
            {
                // старше 30 дней старый сайт сам удалял — переносить незачем
                if (day < cutoff || !QuestionCode.TryParse(raw1, out var code) || !codes.Contains(code) || !existing.Add((day, code)))
                {
                    skipped++;
                    continue;
                }

                study.Add(ReviewMark.Create(day, code, today));
                imported++;
            }
        }

        return new ImportPart(imported, skipped, errors);
    }

    private sealed record LegacyIdea(string? Id, string? D, string? T, string? N, string? P, string? Q, string? S, string? Done);

    private async Task<ImportPart> ImportIdeasAsync(string raw, DateOnly today, CancellationToken ct)
    {
        var errors = new List<string>();
        if (Parse<LegacyIdea[]>(raw, "идеи", errors) is not { } list)
            return new ImportPart(0, 0, errors);

        var known = await notes.GetIdeaLegacyIdsAsync(ct);
        int imported = 0, skipped = 0;
        foreach (var x in list)
        {
            if (string.IsNullOrEmpty(x.Id) || !known.Add(x.Id))
            {
                skipped++;
                continue;
            }

            try
            {
                var idea = Idea.Create(x.T ?? "", TryDay(x.D, out var d) ? d : today, legacyId: x.Id);
                idea.Update(x.T ?? "", x.N, x.P, x.Q);
                var status = x.S switch { "doing" => IdeaStatus.Doing, "done" => IdeaStatus.Done, _ => IdeaStatus.New };
                idea.RestoreStatus(status, TryDay(x.Done, out var done) ? done : null);
                notes.Add(idea);
                imported++;
            }
            catch (DomainException ex)
            {
                errors.Add($"идея «{x.T}»: {ex.Message}");
            }
        }

        return new ImportPart(imported, skipped, errors);
    }

    private sealed record LegacyHighlight(string? Id, string? Sec, string? Text, string? Prefix, string? Suffix, int Start, string? Color);

    private async Task<ImportPart> ImportHighlightsAsync(string raw, CancellationToken ct)
    {
        var errors = new List<string>();
        if (Parse<LegacyHighlight[]>(raw, "выделения", errors) is not { } list)
            return new ImportPart(0, 0, errors);

        var known = await notes.GetHighlightLegacyIdsAsync(ct);
        int imported = 0, skipped = 0;
        foreach (var x in list)
        {
            if (string.IsNullOrEmpty(x.Id) || !known.Add(x.Id))
            {
                skipped++;
                continue;
            }

            try
            {
                var color = Enum.TryParse<HighlightColor>(x.Color, ignoreCase: true, out var c) ? c : HighlightColor.Yellow;
                notes.Add(Highlight.Create(x.Sec ?? "", x.Text ?? "", x.Prefix, x.Suffix, Math.Max(0, x.Start), color, legacyId: x.Id));
                imported++;
            }
            catch (DomainException ex)
            {
                errors.Add($"выделение «{Shorten(x.Text)}»: {ex.Message}");
            }
        }

        return new ImportPart(imported, skipped, errors);
    }

    /// <summary>Календарь открыт и режим 🎯 — в старом сайте «1» / «0».</summary>
    private async Task<bool> ImportSettingsAsync(string? calendarOpen, string? focus, CancellationToken ct)
    {
        if (calendarOpen is null && focus is null)
            return false;

        var settings = await study.GetSettingsAsync(ct);
        if (settings is null)
        {
            settings = UserSettings.CreateDefault();
            study.Add(settings);
        }

        settings.Update(settings.DailyTheoryGoal, settings.WeeklyWorkoutGoal,
            calendarOpen is null ? settings.CalendarOpen : calendarOpen == "1",
            focus is null ? settings.FocusMode : focus == "1",
            settings.TimeZone);
        return true;
    }

    private async Task<ImportPart> ImportJobsAsync(string raw, CancellationToken ct)
    {
        var errors = new List<string>();
        if (Parse<LegacyJob[]>(raw, "отклики", errors) is not { } list)
            return new ImportPart(0, 0, errors);

        var r = await jobs.ImportAsync(list, ct);
        return new ImportPart(r.Imported, r.Skipped, r.Errors);
    }

    private static T? Parse<T>(string raw, string what, List<string> errors) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(raw, Json);
        }
        catch (JsonException ex)
        {
            errors.Add($"{what}: не JSON ({ex.Message})");
            return null;
        }
    }

    private static bool TryDay(string? value, out DateOnly day) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day);

    private static string Shorten(string? text) => text is { Length: > 40 } ? text[..40] + "…" : text ?? "";
}
