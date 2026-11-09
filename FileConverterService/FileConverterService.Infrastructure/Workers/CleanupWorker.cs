using FileConverterService.Application.Jobs;
using FileConverterService.Application.Repositories;
using FileConverterService.Infrastructure.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FileConverterService.Infrastructure.Workers;

public sealed class CleanupWorker(IJobCleanupSchedule schedule, IJobCleanupService cleanup,
    IOptions<CleanupOptions> options, TimeProvider clock, ILogger<CleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        var interval = TimeSpan.FromSeconds(settings.IntervalSeconds);
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var now = clock.GetUtcNow();
                    var jobs = await schedule.GetDueJobIdsAsync(now, settings.BatchSize, stoppingToken);
                    foreach (var jobId in jobs)
                    {
                        try
                        {
                            var deleted = await cleanup.ExecuteAsync(jobId, automatic: true, cancellationToken: stoppingToken);
                            if (!deleted)
                                await DeferRetryAsync(jobId, interval, stoppingToken);
                        }
                        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                        {
                            logger.LogError(exception, "Cleanup failed for job {JobId}; metadata and schedule are retained for retry.", jobId);
                            await DeferRetryAsync(jobId, interval, stoppingToken);
                        }
                    }
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    logger.LogError(exception, "Could not read cleanup schedule; retrying next cycle.");
                }
                await Task.Delay(interval, clock, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    private async Task DeferRetryAsync(Guid jobId, TimeSpan interval, CancellationToken cancellationToken)
    {
        try
        {
            // Leave the next cycle free to process other due jobs. Default: retry after 60 seconds.
            await schedule.DeferAsync(jobId, clock.GetUtcNow().Add(interval + interval), cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Could not postpone cleanup for job {JobId}.", jobId);
        }
    }
}
