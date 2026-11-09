using FileConverterService.Application.Jobs;
using MediatR;

namespace FileConverterService.Application.UseCases.DeleteResourceRelatedToJob;

public class DeleteResourceRelatedToJob_UCHandler(IJobCleanupService cleanup)
    : IRequestHandler<DeleteResourceRelatedToJob_UCInput, DeleteResourceRelatedToJob_UCOutput>
{
    public async Task<DeleteResourceRelatedToJob_UCOutput> Handle(DeleteResourceRelatedToJob_UCInput request, CancellationToken cancellationToken)
    {
        var jobId = Guid.Parse(request.JobId);
        await cleanup.ExecuteAsync(jobId, cancellationToken: cancellationToken);
        return new DeleteResourceRelatedToJob_UCOutput { JobId = jobId.ToString("N"), Success = true };
    }
}
