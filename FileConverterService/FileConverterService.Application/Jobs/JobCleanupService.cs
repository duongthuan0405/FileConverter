using FileConverterService.Application.BusinessExceptions;
using FileConverterService.Application.Constants;
using FileConverterService.Application.HelperServices.Storage;
using FileConverterService.Application.Repositories;
using FileConverterService.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace FileConverterService.Application.Jobs;

public sealed class JobCleanupService(IJobConversionRepository repository, IFileManagerService files,
    IJobCleanupSchedule schedule, ILogger<JobCleanupService> logger) : IJobCleanupService
{
    public async Task<bool> ExecuteAsync(Guid jobId, bool automatic = false, CancellationToken cancellationToken = default)
    {
        var job = await repository.GetByIdAsync(jobId, cancellationToken);
        if (job is null)
        {
            if (!automatic)
                throw new NotFoundException(ApplicationErrorMessage.JobNotFound);
            await schedule.DeleteJobAsync(jobId, cancellationToken);
            return false;
        }

        if (job.Id != jobId)
            throw new InvalidOperationException("Stored job ID does not match cleanup job ID.");
        if (job.Status is not (ConversionJobStatus.Completed or ConversionJobStatus.Failed))
        {
            if (automatic) return false;
            throw new ConflictUniqueValueException(ApplicationErrorMessage.JobIsActive);
        }

        // Terminal jobs have immutable file keys. Repeated/concurrent deletion is safe.
        // Keep metadata and schedule if either file operation fails so a later cycle can retry.
        var t1 = files.DeleteFileAsync(job.OutputStorageKey ?? $"output/{job.Id:N}.pdf", cancellationToken);
        var t2 = files.DeleteFileAsync(job.InputStorageKey, cancellationToken);
        await Task.WhenAll(t1, t2);
        await schedule.DeleteJobAsync(jobId, cancellationToken);
        logger.LogInformation("Deleted resources for job {JobId}.", jobId);
        return true;
    }
}
