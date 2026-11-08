using FileConverterService.Application.HelperServices.Storage;
using FileConverterService.Application.Repositories;
using FileConverterService.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FileConverterService.Application.UseCases.ConvertToPDF;

public class ConverToPDF_UCHandler : IRequestHandler<ConverToPDF_UCInput, ConverToPDF_UCOutput>
{
    private readonly IFileManagerService _fileManagerService;
    private readonly IConversionJobAtomicOperations _jobAtomicOperations;
    private readonly ILogger<ConverToPDF_UCHandler> _logger;

    public ConverToPDF_UCHandler(IFileManagerService fileManagerService, IConversionJobAtomicOperations jobAtomicOperations, ILogger<ConverToPDF_UCHandler> logger)
    {
        _fileManagerService = fileManagerService;
        _jobAtomicOperations = jobAtomicOperations;
        _logger = logger;
    }

    public async Task<ConverToPDF_UCOutput> Handle(ConverToPDF_UCInput request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
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
            await _fileManagerService.SaveFileAtomicallyAsync(p.Content, p.Job.InputStorageKey, cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await _fileManagerService.DeleteFileAsync(p.Job.InputStorageKey, CancellationToken.None);
                }
                catch (Exception cleanupException)
                {
                    _logger.LogError(cleanupException,
                        "Could not delete input {InputStorageKey} for job {JobId} canceled before submission.",
                        p.Job.InputStorageKey, p.Job.Id);
                }
                throw;
            }

            // A failed response does not prove Redis rejected the job.
            // Retain input on submission errors for later reconciliation.
            await _jobAtomicOperations.SubmitAsync(p.Job, CancellationToken.None);
            return p.Job.Id;
        }).ToList();

        int totalCount = 0;
        int successCount = 0;
        List<string> successJobs = null!;
   
        try
        {
            await Task.WhenAll(jobTasks);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogError(ex, "Some files could not be accepted for conversion.");
        }

        totalCount = pendingUploads.Count;
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
