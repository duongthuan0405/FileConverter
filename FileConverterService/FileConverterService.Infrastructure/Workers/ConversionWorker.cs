using FileConverterService.Application.Converters;
using FileConverterService.Application.HelperServices.Storage;
using FileConverterService.Application.Queues;
using FileConverterService.Application.Repositories;
using FileConverterService.Domain.Enums;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FileConverterService.Infrastructure.Workers;

public sealed class ConversionWorker : BackgroundService
{
    private readonly IConversionJobQueue _queue;
    private readonly IJobConversionRepository _repository;
    private readonly IFileManagerService _fileManager;
    private readonly IDocumentConverter _converter;
    private readonly ILogger<ConversionWorker> _logger;

    public ConversionWorker(IConversionJobQueue queue, IJobConversionRepository repository, IFileManagerService fileManager, IDocumentConverter converter, ILogger<ConversionWorker> logger)
    {
        _queue = queue; _repository = repository; _fileManager = fileManager; _converter = converter; _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var jobId in ReadJobs(stoppingToken))
        {
            await Task.Delay(10000);
            try
            {
                var job = await _repository.GetByIdAsync(jobId, stoppingToken);
                if (job is null)
                {
                    _logger.LogWarning("Job {JobId} was not found in the repository.", jobId);
                    continue;
                }

                if (job.Id != jobId)
                    throw new InvalidOperationException($"Stored job ID '{job.Id}' does not match queued ID '{jobId}'.");

                if (job.Status != ConversionJobStatus.Pending)
                {
                    _logger.LogWarning("Skipping job {JobId} in status {Status}.", jobId, job.Status);
                    continue;
                }

                job.Start();
                await _repository.UpdateAsync(job, stoppingToken);
                try
                {
                    await using var input = await _fileManager.ReadFileAsync(job.InputStorageKey, stoppingToken);
                    var outputKey = $"output/{job.Id:N}.pdf";
                    await _converter.ConvertToPdfAsync(
                        job.Id,
                        input,
                        job.OriginalFileName,
                        pdfStream => _fileManager.SaveFileAsync(pdfStream, outputKey, stoppingToken),
                        stoppingToken);
                    job.Complete(outputKey);
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(exception, "Conversion failed for job {JobId}", jobId);
                    job.Fail(exception.Message);
                }

                await _repository.UpdateAsync(job, stoppingToken);
                if (job.Status == ConversionJobStatus.Completed)
                {
                    _logger.LogInformation(
                        "Conversion completed successfully for job {JobId}. Output: {OutputKey}",
                        job.Id, job.OutputStorageKey);
                }
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(exception, "Could not process or persist job {JobId}", jobId);
            }

        }
    }

    private async IAsyncEnumerable<Guid> ReadJobs([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
            yield return await _queue.DequeueAsync(cancellationToken);
    }
}
