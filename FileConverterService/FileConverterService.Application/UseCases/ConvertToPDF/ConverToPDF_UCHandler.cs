using FileConverterService.Application.HelperServices.Storage;
using FileConverterService.Application.Repositories;
using FileConverterService.Application.Queues;
using FileConverterService.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FileConverterService.Application.UseCases.ConvertToPDF;

public class ConverToPDF_UCHandler : IRequestHandler<ConverToPDF_UCInput, ConverToPDF_UCOutput>
{
    private readonly IFileManagerService _fileManagerService;
    private readonly IJobConversionRepository _jobConversionRepository;
    private readonly IConversionJobQueue _conversionQueue;
    private readonly ILogger<ConverToPDF_UCHandler> _logger;

    public ConverToPDF_UCHandler(IFileManagerService fileManagerService, IJobConversionRepository jobConversionRepository, IConversionJobQueue conversionQueue, ILogger<ConverToPDF_UCHandler> logger)
    {
        _fileManagerService = fileManagerService;
        _jobConversionRepository = jobConversionRepository;
        _conversionQueue = conversionQueue;
        _logger = logger;
    }

    public async Task<ConverToPDF_UCOutput> Handle(ConverToPDF_UCInput request, CancellationToken cancellationToken)
    {
        var pendingUploads = request.Files.Select(file =>
        {
            var safeFileName = Path.GetFileName(file.FileName);
            var extension = Path.GetExtension(safeFileName);
            var fileInputName = Guid.NewGuid();
            var storageKey = $"input/{fileInputName:N}{extension}";
            var job = new ConversionJob(safeFileName, file.Type, storageKey);
            return new { Job = job, Content = file.Content };
        }).ToList();

        var jobTasks = pendingUploads.Select(async p =>
        {
            await _fileManagerService.SaveFileAsync(p.Content, p.Job.InputStorageKey, cancellationToken);
            await _jobConversionRepository.AddAsync(p.Job, cancellationToken);
            await _conversionQueue.EnqueueAsync(p.Job.Id, cancellationToken);
            return p.Job.Id;
        }).ToList();

        int totalCount = 0;
        int successCount = 0;
        List<string> successJobs = null!;
   
        try
        {
            await Task.WhenAll(jobTasks);
        }
        catch(Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(ex.Message, ex);
        }

        totalCount = request.Files.Count();
        successJobs = jobTasks.Where(j => j.IsCompletedSuccessfully).Select(j => j.Result.ToString("N")).ToList();
        successCount = successJobs.Count();

        return new ConverToPDF_UCOutput
        {
            TotalFile = totalCount,
            SuccessfulUploadedFile = successCount,
            JobIds = successJobs
        };

    }
}
