using FileConverterService.Application.Converters;
using FileConverterService.Application.HelperServices.Storage;
using FileConverterService.Domain.Entities;
using FileConverterService.Application.Repositories;
using FileConverterService.Domain.Enums;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FileConverterService.Infrastructure.Options;

namespace FileConverterService.Infrastructure.Workers;

public sealed class ConversionJobWorker : BackgroundService
{
    private readonly IConversionJobAtomicOperations _jobAtomicOperations;
    private readonly IJobConversionRepository _repository;
    private readonly IFileManagerService _fileManager;
    private readonly IDocumentConverter _converter;
    private readonly ILogger<ConversionJobWorker> _logger;
    private readonly int _maxConcurrentJobs;
    private readonly TimeSpan _heartbeatInterval;

    public ConversionJobWorker(IConversionJobAtomicOperations jobAtomicOperations, IJobConversionRepository repository, IFileManagerService fileManager, IDocumentConverter converter, IOptions<ConversionWorkerOptions> options, ILogger<ConversionJobWorker> logger)
    {
        _repository = repository; _fileManager = fileManager; _converter = converter; _logger = logger;
        _jobAtomicOperations = jobAtomicOperations;
        _maxConcurrentJobs = options.Value.MaxConcurrentJobs;
        if (_maxConcurrentJobs < 1)
            throw new ArgumentOutOfRangeException(nameof(options), "MaxConcurrentJobs must be greater than zero.");
        if (options.Value.HeartbeatIntervalSeconds is < 1 or > 3600)
            throw new ArgumentOutOfRangeException(nameof(options), "HeartbeatIntervalSeconds must be between 1 and 3600.");
        _heartbeatInterval = TimeSpan.FromSeconds(options.Value.HeartbeatIntervalSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting {MaxConcurrentJobs} concurrent conversion loops.", _maxConcurrentJobs);
        try
        {
            // Fixed async loops bound concurrency without prefetching a batch of jobs.
            await Task.WhenAll(Enumerable.Range(0, _maxConcurrentJobs)
                .Select(_ => ProcessJobsAsync(stoppingToken)));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            
        }
    }

    private async Task ProcessJobsAsync(CancellationToken stoppingToken)
    {
        await foreach (var claim in ReadJobs(stoppingToken))
        {
            var jobId = claim.JobId;
            using var processing = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            using var heartbeat = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            var processingToken = processing.Token;
            var heartbeatTask = RunHeartbeatAsync(claim, processing, heartbeat.Token);
            try
            {
                var job = await _repository.GetByIdAsync(jobId, processingToken);
                if (job is null)
                {
                    _logger.LogWarning("Job {JobId} was not found. Receipt {ReceiptId} retained in processing.", jobId, claim.ReceiptId);
                    continue;
                }

                if (job.Id != jobId)
                    throw new InvalidOperationException($"Stored job ID '{job.Id}' does not match queued ID '{jobId}'.");

                if (job.Status != ConversionJobStatus.Pending)
                {
                    // A terminal state can be left behind if the previous ACK failed.
                    if (job.Status is ConversionJobStatus.Completed or ConversionJobStatus.Failed or ConversionJobStatus.Interrupted)
                        await _jobAtomicOperations.AcknowledgeAsync(claim, processingToken);
                    _logger.LogWarning("Skipping job {JobId} in status {Status}.", jobId, job.Status);
                    continue;
                }

                job.Start();
                await _jobAtomicOperations.MarkProcessingAsync(job, claim, processingToken);
                try
                {
                    await using var input = await _fileManager.ReadFileAsync(job.InputStorageKey, processingToken);
                    var outputKey = $"output/{job.Id:N}.pdf";

                    Func<Stream, CancellationToken, Task> callback = async (stream, cancellationToken) =>
                    {
                        await _fileManager.SaveFileAtomicallyAsync(stream, outputKey, cancellationToken);
                    }; 

                    await _converter.ConvertToPdfAsync(
                        job.Id,
                        input,
                        job.OriginalFileName,
                        callback,
                        processingToken);
                    processingToken.ThrowIfCancellationRequested();
                    job.Complete(outputKey);
                }
                catch (Exception exception) when (!processingToken.IsCancellationRequested)
                {
                    _logger.LogError(exception, "Conversion failed for job {JobId}", jobId);
                    job.Fail(exception.Message);
                }

                processingToken.ThrowIfCancellationRequested();
                await _jobAtomicOperations.SaveTerminalAsync(job, claim, processingToken);
                // Only ACK after terminal metadata and cleanup due time have been persisted.
                await _jobAtomicOperations.AcknowledgeAsync(claim, processingToken);
                if (job.Status == ConversionJobStatus.Completed)
                {
                    _logger.LogInformation(
                        "Conversion completed successfully for job {JobId}. Output: {OutputKey}",
                        job.Id, job.OutputStorageKey);
                }
            }
            catch (OperationCanceledException) when (processingToken.IsCancellationRequested && !stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning("Stopped processing job {JobId} because receipt {ReceiptId} is no longer valid.", jobId, claim.ReceiptId);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(exception, "Could not process or persist job {JobId}. Receipt {ReceiptId} retained in processing.", jobId, claim.ReceiptId);
            }
            finally
            {
                // Join the heartbeat task before disposing its token sources or claiming another job.
                heartbeat.Cancel();
                await heartbeatTask;
            }
        }
    }

    private async Task RunHeartbeatAsync(ConversionJobReceipt claim, CancellationTokenSource processing,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(_heartbeatInterval, cancellationToken);
                try
                {
                    var valid = await _jobAtomicOperations.HeartbeatAsync(claim, cancellationToken);
                    if (!valid && !cancellationToken.IsCancellationRequested)
                    {
                        processing.Cancel();
                        return;
                    }
                }
                catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
                {
                    // A failed Redis call does not prove that the receipt has been revoked.
                    _logger.LogWarning(exception, "Heartbeat failed for receipt {ReceiptId}; retrying next interval.", claim.ReceiptId);
                }
            }
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            // An already issued Redis heartbeat can finish with a transport error during shutdown.
        }
    }

    private async IAsyncEnumerable<ConversionJobReceipt> ReadJobs(
    [System.Runtime.CompilerServices.EnumeratorCancellation]
    CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            ConversionJobReceipt claim;

            try
            {
                claim = await _jobAtomicOperations.ClaimAsync(cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Could not dequeue a job. Retrying in 3 seconds.");

                try
                {
                    await Task.Delay(
                        TimeSpan.FromSeconds(3), cancellationToken);
                }
                catch (OperationCanceledException)
                    when (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }

                continue;
            }

            yield return claim;
        }
    }
}
