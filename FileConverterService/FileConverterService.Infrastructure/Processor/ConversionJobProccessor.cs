using FileConverterService.Application.Converters;
using FileConverterService.Application.HelperServices.Storage;
using FileConverterService.Application.Queues;
using FileConverterService.Application.Repositories;
using FileConverterService.Domain.Enums;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FileConverterService.Infrastructure.Processors;

public sealed class ConversionJobProccessor : BackgroundService
{
    private readonly IConversionJobQueue _queue;
    private readonly IJobConversionRepository _repository;
    private readonly IFileManagerService _fileManager;
    private readonly IDocumentConverter _converter;
    private readonly ILogger<ConversionJobProccessor> _logger;

    public ConversionJobProccessor(IConversionJobQueue queue, IJobConversionRepository repository, IFileManagerService fileManager, IDocumentConverter converter, ILogger<ConversionJobProccessor> logger)
    {
        _queue = queue; _repository = repository; _fileManager = fileManager; _converter = converter; _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
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
                        await _queue.AcknowledgeAsync(claim, stoppingToken);
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
                await _queue.AcknowledgeAsync(claim, stoppingToken);
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

    private async IAsyncEnumerable<ConversionJobClaim> ReadJobs([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
            yield return await _queue.DequeueAsync(cancellationToken);
    }
}
