using FileConverterService.Application.HelperServices.Storage;
using FileConverterService.Application.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;
using FileConverterService.Application.BusinessExceptions;
using FileConverterService.Application.Constants;
using FileConverterService.Domain.Enums;

namespace FileConverterService.Application.UseCases.GetFileOfJob;

public class GetFileOfJob_UCHandler : IRequestHandler<GetFileOfJob_UCInput, GetFileOfJob_UCOutput>
{
    private readonly IJobConversionRepository _jobRepo;
    private readonly IFileManagerService _fileManager;
    private readonly ILogger<GetFileOfJob_UCHandler> _logger;

    public GetFileOfJob_UCHandler(IJobConversionRepository jobRepo, IFileManagerService fileManager, ILogger<GetFileOfJob_UCHandler> logger)
    {
        _jobRepo = jobRepo;
        _fileManager = fileManager;
        _logger = logger;
    }

    public async Task<GetFileOfJob_UCOutput> Handle(GetFileOfJob_UCInput request, CancellationToken cancellationToken)
    {
        var jobId = Guid.Parse(request.JobId);
        var job = await _jobRepo.GetByIdAsync(jobId, cancellationToken)
            ?? throw new NotFoundException(ApplicationErrorMessage.JobNotFound);

        if (job.Status != ConversionJobStatus.Completed)
            throw new ConflictUniqueValueException(ApplicationErrorMessage.JobNotCompleted);

        if (string.IsNullOrWhiteSpace(job.OutputStorageKey))
            throw new NotFoundException(ApplicationErrorMessage.JobFileNotFound);

        // Resolve the download name before opening the stream; the HTTP response owns it.
        var originalName = job.OriginalFileName.Replace('\\', '/');
        var fileName = Path.ChangeExtension(Path.GetFileName(originalName), ".pdf");
        Stream content;
        try
        {
            content = await _fileManager.ReadFileAsync(job.OutputStorageKey, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            throw new NotFoundException(ApplicationErrorMessage.JobFileNotFound);
        }
        catch (DirectoryNotFoundException)
        {
            throw new NotFoundException(ApplicationErrorMessage.JobFileNotFound);
        }

        return new GetFileOfJob_UCOutput
        {
            Content = content,
            FileName = fileName,
            ContentType = "application/pdf"
        };
    }
}

