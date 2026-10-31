namespace FileConverterService.Application.Queues;

public interface IConversionJobQueue
{
    Task EnqueueAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task<Guid> DequeueAsync(CancellationToken cancellationToken = default);
}
