using FileConverterService.Application.HelperServices.Storage;
using FileConverterService.Application.Repositories;
using FileConverterService.Domain.Entities;
using FileConverterService.Application.BusinessExceptions;
using FileConverterService.Application.Constants;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FileConverterService.Application.UseCases.GetJob;

public class GetJob_UCHandler : IRequestHandler<GetJob_UCInput, GetJob_UCOutput>
{
    private readonly IJobConversionRepository _jobRepo;
    private readonly ILogger<GetJob_UCHandler> _logger;

    public GetJob_UCHandler(IJobConversionRepository jobRepo, ILogger<GetJob_UCHandler> logger)
    {
        _jobRepo = jobRepo;
        _logger = logger;
    }

    public async Task<GetJob_UCOutput> Handle(GetJob_UCInput request, CancellationToken cancellationToken)
    {
        var jobId = Guid.Parse(request.JobId);

        var job = await _jobRepo.GetByIdAsync(jobId, cancellationToken)
            ?? throw new NotFoundException(ApplicationErrorMessage.JobNotFound);

        return new GetJob_UCOutput
        {
            JobId = job.Id.ToString("N"),
            OriginalFileName = job.OriginalFileName,
            Status = job.Status.ToString(),
            CreatedAt = job.CreatedAt,
            CompletedAt = job.CompletedAt,
            ProcessedAt = job.ProccessedAt,
            Error = job.Error
        };
    }
}

