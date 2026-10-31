using FileConverterService.Application.Queues;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace FileConverterService.Infrastructure.Queues;

/// <summary>
/// FIFO queue with the same destructive dequeue semantics as ChannelConversionQueue.
/// Job metadata and recovery of jobs already dequeued are managed separately.
/// </summary>
public sealed class RedisConversionQueue : IConversionJobQueue
{
    private readonly IDatabase _redisDb;
    private readonly RedisKey _queueKey;
    private readonly TimeSpan _pollInterval;

    public RedisConversionQueue(IConnectionMultiplexer connection, IOptions<RedisOptions> options)
    {
        _redisDb = connection.GetDatabase();
        _queueKey = options.Value.QueueKey;
        _pollInterval = TimeSpan.FromMilliseconds(options.Value.PollIntervalMilliseconds);
    }

    public async Task EnqueueAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Await the actual write: cancellation must not hide a successful enqueue.
        await _redisDb.ListLeftPushAsync(_queueKey, jobId.ToString("N"));
    }

    public async Task<Guid> DequeueAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Do not cancel the await after issuing a destructive pop: a returned job
            // must reach the caller rather than disappear on cancellation.
            var value = await _redisDb.ListRightPopAsync(_queueKey);
            if (!value.IsNull)
            {
                if (Guid.TryParseExact(value.ToString(), "N", out var jobId))
                    return jobId;

                throw new InvalidOperationException($"Redis queue '{_queueKey}' contains an invalid job ID.");
            }

            await Task.Delay(_pollInterval, cancellationToken);
        }
    }
}
