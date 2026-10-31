using FileConverterService.Application.Converters;
using FileConverterService.Application.HelperServices.Storage;
using FileConverterService.Application.Queues;
using FileConverterService.Application.Repositories;
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
            var job = await _repository.GetByIdAsync(jobId, stoppingToken);
            if (job is null) continue;

            try
            {
                job.Start();
                await using var input = await _fileManager.ReadFileAsync(job.InputStorageKey, stoppingToken);
                var outputKey = $"output/{job.Id:N}.pdf";
                await _converter.ConvertToPdfAsync(
                    job.Id,
                    input,
                    job.OriginalFileName,
                    pdfStream => _fileManager.SaveFileAsync(pdfStream, outputKey, stoppingToken),
                    stoppingToken);
                job.Complete(outputKey);
                _logger.LogInformation(
                    "Conversion completed successfully for job {JobId}. Output: {OutputKey}",
                    job.Id,
                    outputKey);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(exception, "Conversion failed for job {JobId}", jobId);
                job.Fail(exception.Message);
            }

            await _repository.UpdateAsync(job, stoppingToken);
        }
    }

    private async IAsyncEnumerable<Guid> ReadJobs([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
            yield return await _queue.DequeueAsync(cancellationToken);
    }
}
