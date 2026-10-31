using FileConverterService.Application.HelperServices.Storage;
using FileConverterService.Application.Repositories;
using FileConverterService.Application.Queues;
using FileConverterService.Domain.Entities;
using MediatR;

namespace FileConverterService.Application.UseCases.ConvertToPDF;

public class ConverToPDF_UCHandler : IRequestHandler<ConverToPDF_UCInput, ConverToPDF_UCOutput>
{
    private readonly IFileManagerService _fileManagerService;
    private readonly IJobConversionRepository _jobConversionRepository;
    private readonly IConversionJobQueue _conversionQueue;

    public ConverToPDF_UCHandler(IFileManagerService fileManagerService, IJobConversionRepository jobConversionRepository, IConversionJobQueue conversionQueue)
    {
        _fileManagerService = fileManagerService;
        _jobConversionRepository = jobConversionRepository;
        _conversionQueue = conversionQueue;
    }

    public async Task<ConverToPDF_UCOutput> Handle(ConverToPDF_UCInput request, CancellationToken cancellationToken)
    {
        var pendingUploads = request.Files.Select(file =>
        {
            var safeFileName = Path.GetFileName(file.FileName);
            var extension = Path.GetExtension(safeFileName);
            var jobId = Guid.NewGuid();
            var storageKey = $"input/{jobId:N}{extension}";
            var job = new ConversionJob(safeFileName, file.Type, storageKey);
            return new { Job = job, Content = file.Content };
        }).ToList();

        var saveTasks = pendingUploads.Select(upload => _fileManagerService.SaveFileAsync(upload.Content, upload.Job.InputStorageKey, cancellationToken));
        var addJobsTasks = pendingUploads.Select(upload => _jobConversionRepository.AddAsync(upload.Job, cancellationToken));

        await Task.WhenAll(saveTasks);
        await Task.WhenAll(addJobsTasks);
        await Task.WhenAll(pendingUploads.Select(upload => _conversionQueue.EnqueueAsync(upload.Job.Id, cancellationToken)));

        return new ConverToPDF_UCOutput { JobIds = pendingUploads.Select(upload => upload.Job.Id.ToString("N")).ToList() };
    }
}
