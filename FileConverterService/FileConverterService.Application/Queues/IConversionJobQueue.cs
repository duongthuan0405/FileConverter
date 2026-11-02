namespace FileConverterService.Application.Queues;

public interface IConversionJobQueue
{
    Task EnqueueAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<ConversionJobClaim> DequeueAsync(CancellationToken cancellationToken = default);
    Task AcknowledgeAsync(ConversionJobClaim claim, CancellationToken cancellationToken = default);
}
