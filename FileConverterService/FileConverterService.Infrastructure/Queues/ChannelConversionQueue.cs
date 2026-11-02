using System.Threading.Channels;
using System.Collections.Concurrent;
using FileConverterService.Application.Queues;

namespace FileConverterService.Infrastructure.Queues;

public sealed class ChannelConversionQueue : IConversionJobQueue
{
    private readonly Channel<Guid> _queue = Channel.CreateUnbounded<Guid>();
    // In-memory alternative only: receipts do not survive a process restart.
    private readonly ConcurrentDictionary<Guid, Guid> _processing = new();
    public Task EnqueueAsync(Guid jobId, CancellationToken cancellationToken = default) => _queue.Writer.WriteAsync(jobId, cancellationToken).AsTask();
    public async Task<ConversionJobClaim> DequeueAsync(CancellationToken cancellationToken = default)
    {
        var jobId = await _queue.Reader.ReadAsync(cancellationToken);
        var claim = new ConversionJobClaim(jobId, Guid.NewGuid());
        _processing[claim.ReceiptId] = jobId;
        return claim;
    }

    public Task AcknowledgeAsync(ConversionJobClaim claim, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _processing.TryRemove(new KeyValuePair<Guid, Guid>(claim.ReceiptId, claim.JobId));
        return Task.CompletedTask;
    }
}
