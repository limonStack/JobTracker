using JobTracker.Application.Abstractions;
using JobTracker.Application.VacancyChecking;
using Microsoft.Extensions.Options;

namespace JobTracker.Api.BackgroundJobs;

/// <summary>
/// Фоновая проверка вакансий. Два цикла: таймер раз в Interval ставит проверку в очередь,
/// потребитель забирает запросы из очереди (от таймера и от кнопки) и выполняет по одному.
/// </summary>
internal sealed partial class VacancyCheckWorker(
    IServiceScopeFactory scopeFactory,
    IVacancyCheckQueue queue,
    VacancyCheckProgress progress,
    IOptions<VacancyCheckOptions> options,
    TimeProvider time,
    ILogger<VacancyCheckWorker> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.Value.CheckOnStartup)
            queue.RequestCheckAll();

        return options.Value.ScheduleEnabled
            ? Task.WhenAll(ScheduleAsync(stoppingToken), ConsumeAsync(stoppingToken))
            : ConsumeAsync(stoppingToken);
    }

    private async Task ScheduleAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(options.Value.Interval, time);
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
                queue.RequestCheckAll();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // приложение останавливается
        }
    }

    private async Task ConsumeAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var _ in queue.ReadAllAsync(ct))
                await RunCheckAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // приложение останавливается
        }
    }

    private async Task RunCheckAsync(CancellationToken ct)
    {
        progress.Started();
        CheckAllResult? result = null;
        try
        {
            // воркер — singleton, а DbContext — scoped: на каждый обход свой scope,
            // иначе один DbContext жил бы всё время работы приложения (captive dependency)
            await using var scope = scopeFactory.CreateAsyncScope();
            result = await scope.ServiceProvider.GetRequiredService<VacancyCheckService>().CheckAllAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // сбой одного обхода не должен останавливать воркер — следующий будет по таймеру
            LogRunFailed(ex);
        }
        finally
        {
            progress.Finished(time.GetUtcNow(), result);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Фоновая проверка вакансий завершилась ошибкой")]
    private partial void LogRunFailed(Exception exception);
}
