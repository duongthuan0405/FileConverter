using FileConverterService.Application.Converters;
using FileConverterService.Application.Jobs;
using FileConverterService.Application.HelperServices.Storage;
using FileConverterService.Application.Queues;
using FileConverterService.Application.Repositories;
using FileConverterService.Domain.Enums;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FileConverterService.Infrastructure.Workers;

public sealed class ConversionJobWorker : BackgroundService
{
    private readonly IConversionJobAtomicOperations _jobAtomicOperations;
    private readonly IJobConversionRepository _repository;
    private readonly IFileManagerService _fileManager;
    private readonly IDocumentConverter _converter;
    private readonly ILogger<ConversionJobWorker> _logger;
    private readonly int _maxConcurrentJobs;

    public ConversionJobWorker(IConversionJobAtomicOperations jobAtomicOperations, IJobConversionRepository repository, IFileManagerService fileManager, IDocumentConverter converter, IOptions<ConversionWorkerOptions> options, ILogger<ConversionJobWorker> logger)
    {
        _repository = repository; _fileManager = fileManager; _converter = converter; _logger = logger;
        _jobAtomicOperations = jobAtomicOperations;
        _maxConcurrentJobs = options.Value.MaxConcurrentJobs;
        if (_maxConcurrentJobs < 1)
            throw new ArgumentOutOfRangeException(nameof(options), "MaxConcurrentJobs must be greater than zero.");
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
            try
            {
                var job = await _repository.GetByIdAsync(jobId, stoppingToken);
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
                    if (job.Status is ConversionJobStatus.Completed or ConversionJobStatus.Failed)
                        await _jobAtomicOperations.AcknowledgeAsync(claim, stoppingToken);
                    _logger.LogWarning("Skipping job {JobId} in status {Status}.", jobId, job.Status);
                    continue;
                }

                job.Start();
                await _repository.UpdateAsync(job, stoppingToken);
                try
                {
                    await using var input = await _fileManager.ReadFileAsync(job.InputStorageKey, stoppingToken);
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
                        stoppingToken);
                    job.Complete(outputKey);
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(exception, "Conversion failed for job {JobId}", jobId);
                    job.Fail(exception.Message);
                }

                await _repository.UpdateAsync(job, stoppingToken);
                // Only ACK after Complete/Fail has been persisted, not inside the save callback.
                await _jobAtomicOperations.AcknowledgeAsync(claim, stoppingToken);
                if (job.Status == ConversionJobStatus.Completed)
                {
                    _logger.LogInformation(
                        "Conversion completed successfully for job {JobId}. Output: {OutputKey}",
                        job.Id, job.OutputStorageKey);
                }
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(exception, "Could not process or persist job {JobId}. Receipt {ReceiptId} retained in processing.", jobId, claim.ReceiptId);
            }

        }
    }

    private async IAsyncEnumerable<ConversionJobClaim> ReadJobs(
    [System.Runtime.CompilerServices.EnumeratorCancellation]
    CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            ConversionJobClaim claim;

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
