using FileConverterService.Application.Repositories;
using FileConverterService.Application.HelperServices.Storage;
using FileConverterService.Application.BusinessExceptions;
using FileConverterService.Application.Constants;
using FileConverterService.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FileConverterService.Application.UseCases.DeleteResourceRelatedToJob;

public class DeleteResourceRelatedToJob_UCHandler : IRequestHandler<DeleteResourceRelatedToJob_UCInput, DeleteResourceRelatedToJob_UCOutput>
{
    private readonly IJobConversionRepository _jobRepo;
    private readonly IFileManagerService _fileManager;
    private readonly ILogger<DeleteResourceRelatedToJob_UCHandler> _logger;

    public DeleteResourceRelatedToJob_UCHandler(IJobConversionRepository jobRepo, IFileManagerService fileManager, ILogger<DeleteResourceRelatedToJob_UCHandler> logger)
    {
        _jobRepo = jobRepo;
        _fileManager = fileManager;
        _logger = logger;
    }

    public async Task<DeleteResourceRelatedToJob_UCOutput> Handle(DeleteResourceRelatedToJob_UCInput request, CancellationToken cancellationToken)
    {
        var jobId = Guid.Parse(request.JobId);

        var job = await _jobRepo.GetByIdAsync(jobId, cancellationToken)
            ?? throw new NotFoundException(ApplicationErrorMessage.JobNotFound);

        if (job.Status is ConversionJobStatus.Pending or ConversionJobStatus.Processing)
            throw new ConflictUniqueValueException(ApplicationErrorMessage.JobIsActive);

        // A failed conversion can leave a partial PDF before OutputStorageKey is set.
        var outputKey = job.OutputStorageKey ?? $"output/{job.Id:N}.pdf";
        await _fileManager.DeleteFileAsync(outputKey, cancellationToken);
        await _fileManager.DeleteFileAsync(job.InputStorageKey, cancellationToken);
        await _jobRepo.DeleteAsync(job, cancellationToken);

        _logger.LogInformation("Deleted resources for job {JobId}.", jobId);
        return new DeleteResourceRelatedToJob_UCOutput
        {
            JobId = job.Id.ToString("N"),
            Success = true
        };
    }
}

