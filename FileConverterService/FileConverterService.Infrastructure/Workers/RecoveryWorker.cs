using FileConverterService.Application.Repositories;
using FileConverterService.Infrastructure.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FileConverterService.Infrastructure.Workers;

public sealed class RecoveryWorker(IConversionJobAtomicOperations jobs, IOptions<RecoveryOptions> options,
    TimeProvider clock, ILogger<RecoveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var staleBefore = clock.GetUtcNow().AddSeconds(-settings.HeartbeatTimeoutSeconds);
                    await foreach (var claim in jobs.ScanReceiptsAsync(settings.ScanPageSize, stoppingToken))
                    {
                        if (claim.LastHeartbeatAt > staleBefore) continue;
                        try
                        {
                            if (await jobs.TryRecoverAsync(claim, staleBefore, stoppingToken))
                                logger.LogInformation("Recovered stale receipt {ReceiptId} for job {JobId}.", claim.ReceiptId, claim.JobId);
                        }
                        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                        {
                            logger.LogError(exception, "Could not recover receipt {ReceiptId}; retrying a later cycle.", claim.ReceiptId);
                        }
                    }
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    logger.LogError(exception, "Could not scan receipts for recovery; retrying next cycle.");
                }
                await Task.Delay(TimeSpan.FromSeconds(settings.IntervalSeconds), clock, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
