using JobTracker.Application.Abstractions;
using JobTracker.Domain.Knowledge;
using JobTracker.Domain.Study;
using Microsoft.EntityFrameworkCore;

namespace JobTracker.Infrastructure.Persistence;

// фильтр по пользователю наложен в DbContext — здесь о владельце думать не нужно
internal sealed class StudyRepository(JobTrackerDbContext db) : IStudyRepository
{
    public async Task<IReadOnlyList<StudyMark>> ListMarksAsync(CancellationToken ct) =>
        await db.Set<StudyMark>().AsNoTracking().ToListAsync(ct);

    public Task<StudyMark?> GetMarkAsync(QuestionCode code, CancellationToken ct) =>
        db.Set<StudyMark>().FirstOrDefaultAsync(m => m.Code == code, ct);

    public void Add(StudyMark mark) => db.Add(mark);

    public void Remove(StudyMark mark) => db.Remove(mark);

    public async Task<IReadOnlyList<ActivityDay>> ListActivitiesAsync(CancellationToken ct) =>
        await db.Set<ActivityDay>().AsNoTracking().ToListAsync(ct);

    public Task<ActivityDay?> GetActivityAsync(ActivityKind kind, DateOnly day, CancellationToken ct) =>
        db.Set<ActivityDay>().FirstOrDefaultAsync(a => a.Kind == kind && a.Day == day, ct);

    public void Add(ActivityDay activity) => db.Add(activity);

    public void Remove(ActivityDay activity) => db.Remove(activity);

    public async Task<IReadOnlyList<ReviewMark>> ListReviewsAsync(CancellationToken ct) =>
        await db.Set<ReviewMark>().AsNoTracking().ToListAsync(ct);

    public Task<ReviewMark?> GetReviewAsync(DateOnly day, QuestionCode code, CancellationToken ct) =>
        db.Set<ReviewMark>().FirstOrDefaultAsync(r => r.Day == day && r.Code == code, ct);

    public void Add(ReviewMark review) => db.Add(review);

    public void Remove(ReviewMark review) => db.Remove(review);

    public Task RemoveReviewsBeforeAsync(DateOnly day, CancellationToken ct) =>
        db.Set<ReviewMark>().Where(r => r.Day < day).ExecuteDeleteAsync(ct);

    public Task<UserSettings?> GetSettingsAsync(CancellationToken ct) =>
        db.Set<UserSettings>().FirstOrDefaultAsync(ct);

    public void Add(UserSettings settings) => db.Add(settings);
}
