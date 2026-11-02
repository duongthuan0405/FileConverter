using FileConverterService.Application.Queues;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace FileConverterService.Infrastructure.Queues;

/// <summary>
/// FIFO pending list with atomic transfer to a processing hash before delivery.
/// Processing receipts remain until explicitly acknowledged; no automatic replay on restart.
/// </summary>
public sealed class RedisConversionQueue : IConversionJobQueue
{
    private readonly IDatabase _redisDb;
    private readonly RedisKey _queueKey;
    private readonly RedisKey _receiptKey;
    private readonly TimeSpan _pollInterval;
    private const string ClaimScript = """
        local value = redis.call('LINDEX', KEYS[1], -1)
        if not value then return false end
        if redis.call('HEXISTS', KEYS[2], ARGV[1]) == 1 then
            return redis.error_reply('Receipt ID already exists')
        end
        redis.call('HSET', KEYS[2], ARGV[1], value)
        redis.call('RPOP', KEYS[1])
        return value
        """;
    private const string AcknowledgeScript = """
        if redis.call('HGET', KEYS[1], ARGV[1]) == ARGV[2] then
            return redis.call('HDEL', KEYS[1], ARGV[1])
        end
        return 0
        """;

    public RedisConversionQueue(IConnectionMultiplexer connection, IOptions<RedisOptions> options)
    {
        _redisDb = connection.GetDatabase();
        _queueKey = options.Value.QueueKey;
        _receiptKey = options.Value.ReceiptKey;
        _pollInterval = TimeSpan.FromMilliseconds(options.Value.PollIntervalMilliseconds);
    }

    public async Task EnqueueAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Await the actual write: cancellation must not hide a successful enqueue.
        await _redisDb.ListLeftPushAsync(_queueKey, jobId.ToString("N"));
    }

    public async Task<ConversionJobClaim> DequeueAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var receiptId = Guid.NewGuid();
            // Persist the receipt before removing pending. Do not abandon an issued mutation
            // on cancellation: a delivered job must reach the Worker or remain in processing.
            var value = await _redisDb.ScriptEvaluateAsync(
                ClaimScript, [_queueKey, _receiptKey], [receiptId.ToString("N")]);
            if (!value.IsNull)
            {
                if (Guid.TryParseExact(value.ToString(), "N", out var jobId))
                    return new ConversionJobClaim(jobId, receiptId);

                throw new InvalidOperationException(
                    $"Invalid job ID retained in '{_receiptKey}', receipt '{receiptId:N}'.");
            }

            await Task.Delay(_pollInterval, cancellationToken);
        }
    }

    public async Task AcknowledgeAsync(ConversionJobClaim claim, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Conditional delete makes repeated ACKs harmless and rejects mismatched receipts.
        await _redisDb.ScriptEvaluateAsync(AcknowledgeScript, [_receiptKey],
            [claim.ReceiptId.ToString("N"), claim.JobId.ToString("N")]);
    }
}
