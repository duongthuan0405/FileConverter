using System.Threading.Channels;
using FileConverterService.Application.Queues;

namespace FileConverterService.Infrastructure.Queues;

public sealed class ChannelConversionQueue : IConversionJobQueue
{
    private readonly Channel<Guid> _queue = Channel.CreateUnbounded<Guid>();
    public Task EnqueueAsync(Guid jobId, CancellationToken cancellationToken = default) => _queue.Writer.WriteAsync(jobId, cancellationToken).AsTask();
    public Task<Guid> DequeueAsync(CancellationToken cancellationToken = default) => _queue.Reader.ReadAsync(cancellationToken).AsTask();
}
