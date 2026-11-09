using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using FileConverterService.Application.Repositories;
using FileConverterService.Domain.Entities;
using FileConverterService.Domain.Enums;
using FileConverterService.Infrastructure.Extensions;
using FileConverterService.Infrastructure.Options;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace FileConverterService.Infrastructure.Repositories;

public sealed class RedisConversionJobAtomicOperations : IConversionJobAtomicOperations
{
    private readonly IDatabase _database;
    private readonly IConnectionMultiplexer _connection;
    private readonly string _jobKeyPrefix;
    private readonly RedisKey _queueKey;
    private readonly RedisKey _receiptKey;
    private readonly TimeSpan _pollInterval;
    private readonly RedisKey _cleanupKey;
    private readonly int _retentionMinutes;
    private readonly int _interruptedRetentionMinutes;

    private const string RecoverScript = """
        if redis.call('HGET', KEYS[1], 'JobId') ~= ARGV[1] then return 0 end
        local heartbeat = tonumber(redis.call('HGET', KEYS[1], 'LastHeartbeatAt'))
        if not heartbeat or heartbeat > tonumber(ARGV[2]) then return 0 end
        if redis.call('EXISTS', KEYS[2]) == 0 then
            return redis.call('DEL', KEYS[1])
        end
        local status = redis.call('HGET', KEYS[2], 'Status')
        if status == '0' then
            -- Check queue state before removing the only record of this delivery.
            if not redis.call('LPOS', KEYS[3], ARGV[1]) then
                redis.call('LPUSH', KEYS[3], ARGV[1])
            end
            return redis.call('DEL', KEYS[1])
        end
        if status == '1' then
            local cleanupType = redis.call('TYPE', KEYS[4]).ok
            if cleanupType ~= 'none' and cleanupType ~= 'zset' then
                return redis.error_reply('Cleanup key must be a sorted set')
            end
            redis.call('ZADD', KEYS[4], ARGV[5], ARGV[1])
            redis.call('HSET', KEYS[2], 'Status', '4', 'Error', ARGV[3], 'CompletedAt', ARGV[4])
            return redis.call('DEL', KEYS[1])
        end
        if status == '2' or status == '3' or status == '4' then
            return redis.call('DEL', KEYS[1])
        end
        -- Unknown states are left untouched. Interrupted jobs are never requeued.
        return 0
        """;

    private const string MarkProcessingScript = """
        if redis.call('HGET', KEYS[2], 'JobId') ~= ARGV[1] then return -2 end
        if redis.call('HGET', KEYS[1], 'Status') ~= '0' then return -1 end
        redis.call('HSET', KEYS[1], 'Status', '1', 'ProccessedAt', ARGV[2], 'Error', 'null')
        return 1
        """;

    private const string SaveTerminalScript = """
        if redis.call('HGET', KEYS[3], 'JobId') ~= ARGV[1] then return -2 end
        local status = redis.call('HGET', KEYS[1], 'Status')
        if not status then return -1 end
        if status ~= '1' and status ~= ARGV[3] then return -1 end
        if status == ARGV[3] and redis.call('HGET', KEYS[1], 'CompletedAt') ~= ARGV[4] then return -1 end
        local cleanupType = redis.call('TYPE', KEYS[2]).ok
        if cleanupType ~= 'none' and cleanupType ~= 'zset' then
            return redis.error_reply('Cleanup key must be a sorted set')
        end
        redis.call('ZADD', KEYS[2], ARGV[2], ARGV[1])
        redis.call('HSET', KEYS[1], unpack(ARGV, 5))
        return 1
        """;

    private const string HeartbeatScript = """
        if redis.call('HGET', KEYS[1], 'JobId') ~= ARGV[1] then
            return 0
        end
        redis.call('HSET', KEYS[1], 'LastHeartbeatAt', ARGV[2])
        return 1
        """;

    private const string AcknowledgeScript = """
        if redis.call('HGET', KEYS[1], 'JobId') == ARGV[1] then
            return redis.call('DEL', KEYS[1])
        end
        return 0
        """;

    private const string ClaimScript = """
        local value = redis.call('LINDEX', KEYS[1], -1)
        if not value then return false end
        if redis.call('EXISTS', KEYS[2]) == 1 then
            return redis.error_reply('Receipt ID already exists')
        end
        redis.call('HSET', KEYS[2], 'JobId', value, 'LastHeartbeatAt', ARGV[1])
        redis.call('RPOP', KEYS[1])
        return value
        """;

    private const string SubmitScript = """
        local jobType = redis.call('TYPE', KEYS[1]).ok
        local queueType = redis.call('TYPE', KEYS[2]).ok
        if jobType ~= 'none' and jobType ~= 'hash' then
            return redis.error_reply('Job key must be a hash')
        end
        if queueType ~= 'none' and queueType ~= 'list' then
            return redis.error_reply('Queue key must be a list')
        end
        if #ARGV < 4 or (#ARGV - 2) % 2 ~= 0 then
            return redis.error_reply('Invalid metadata arguments')
        end

        if jobType == 'hash' then
            local fingerprint = redis.call('HGET', KEYS[1], '_SubmissionFingerprint')
            if not fingerprint then return -2 end
            if fingerprint ~= ARGV[2] then return -1 end
            return 0
        end

        redis.call('HSET', KEYS[1], '_SubmissionFingerprint', ARGV[2], unpack(ARGV, 3))
        local pushed = redis.pcall('LPUSH', KEYS[2], ARGV[1])
        if type(pushed) == 'table' and pushed.err then
            -- Redis scripts do not roll back writes automatically.
            -- No other client can see this new hash before the script ends.
            redis.call('DEL', KEYS[1])
            return redis.error_reply(pushed.err)
        end
        return 1
        """;

    public RedisConversionJobAtomicOperations(IConnectionMultiplexer connection, IOptions<RedisOptions> options,
        IOptions<CleanupOptions> cleanupOptions)
    {
        _connection = connection;
        _database = connection.GetDatabase();
        _jobKeyPrefix = options.Value.JobKey;
        _queueKey = options.Value.QueueKey;
        _receiptKey = options.Value.ReceiptKey;
        _pollInterval = TimeSpan.FromMilliseconds(options.Value.PollIntervalMilliseconds);
        _cleanupKey = options.Value.CleanupKey;
        _retentionMinutes = cleanupOptions.Value.RetentionMinutes;
        _interruptedRetentionMinutes = cleanupOptions.Value.InterruptedRetentionMinutes;
    }

    public async IAsyncEnumerable<ConversionJobReceipt> ScanReceiptsAsync(int pageSize,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);
        var prefix = _receiptKey.ToString() + ":";
        foreach (var endpoint in _connection.GetEndPoints())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var server = _connection.GetServer(endpoint);
            if (server.IsReplica) continue;
            await foreach (var key in server.KeysAsync(_database.Database, prefix + "*", pageSize: pageSize)
                .WithCancellation(cancellationToken))
            {
                var name = key.ToString();
                if (!name.StartsWith(prefix, StringComparison.Ordinal)
                    || !Guid.TryParseExact(name[prefix.Length..], "N", out var receiptId)) continue;
                var fields = await _database.HashGetAsync(key, [(RedisValue)"JobId", "LastHeartbeatAt"])
                    .WaitAsync(cancellationToken);
                if (!Guid.TryParseExact(fields[0].ToString(), "N", out var jobId)
                    || !long.TryParse(fields[1].ToString(), out var milliseconds)
                    || milliseconds is < -62135596800000 or > 253402300799999) continue;
                yield return new ConversionJobReceipt(jobId, receiptId, DateTimeOffset.FromUnixTimeMilliseconds(milliseconds));
            }
        }
    }

    public async Task<bool> TryRecoverAsync(ConversionJobReceipt claim, DateTimeOffset staleBefore,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claim);
        cancellationToken.ThrowIfCancellationRequested();
        // Recovery, heartbeat and state transitions serialize in Redis. Await an issued mutation.
        var interruptedAt = DateTimeOffset.UtcNow;
        var result = (long)await _database.ScriptEvaluateAsync(RecoverScript,
            [GetReceiptKey(claim.ReceiptId), (RedisKey)$"{_jobKeyPrefix}:{claim.JobId}", _queueKey, _cleanupKey],
            [claim.JobId.ToString("N"), staleBefore.ToUnixTimeMilliseconds(),
                JsonSerializer.Serialize("Job processing was interrupted because its receipt heartbeat timed out. Conversion outcome is unknown."),
                JsonSerializer.Serialize(interruptedAt),
                interruptedAt.AddMinutes(_interruptedRetentionMinutes).ToUnixTimeMilliseconds()]);
        return result == 1;
    }

    public async Task MarkProcessingAsync(ConversionJob job, ConversionJobReceipt claim, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(claim);
        if (claim.JobId != job.Id || claim.ReceiptId == Guid.Empty)
            throw new ArgumentException("Claim must have a non-empty receipt ID and match the job.", nameof(claim));
        if (job.Id == Guid.Empty || job.Status != ConversionJobStatus.Processing || job.ProccessedAt is null)
            throw new ArgumentException("Call Start on the job before persisting Processing.", nameof(job));

        cancellationToken.ThrowIfCancellationRequested();
        // The metadata and receipt must share a hash slot when using Redis Cluster.
        var result = (long)await _database.ScriptEvaluateAsync(MarkProcessingScript,
            [(RedisKey)$"{_jobKeyPrefix}:{job.Id}", GetReceiptKey(claim.ReceiptId)],
            [job.Id.ToString("N"), JsonSerializer.Serialize(job.ProccessedAt)]);
        if (result == -2)
            throw new InvalidOperationException($"Cannot start job '{job.Id}': receipt '{claim.ReceiptId}' is missing or does not match the job.");
        if (result < 0)
            throw new InvalidOperationException($"Cannot start job '{job.Id}': stored job is missing or is not Pending.");
    }

    public async Task SaveTerminalAsync(ConversionJob job, ConversionJobReceipt claim, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(claim);
        if (claim.JobId != job.Id || claim.ReceiptId == Guid.Empty)
            throw new ArgumentException("Claim must have a non-empty receipt ID and match the job.", nameof(claim));
        if (job.Id == Guid.Empty || job.Status is not (ConversionJobStatus.Completed or ConversionJobStatus.Failed)
            || job.CompletedAt is null)
            throw new ArgumentException("Only completed or failed jobs with CompletedAt can be saved.", nameof(job));

        RedisKey jobKey = $"{_jobKeyPrefix}:{job.Id}";
        if (jobKey == _cleanupKey || _cleanupKey == _queueKey || _cleanupKey == _receiptKey)
            throw new InvalidOperationException("Cleanup key must be distinct from job, queue and receipt keys.");
        var arguments = new List<RedisValue>
        {
            job.Id.ToString("N"),
            job.CompletedAt.Value.AddMinutes(_retentionMinutes).ToUnixTimeMilliseconds(),
            JsonSerializer.Serialize(job.Status),
            JsonSerializer.Serialize(job.CompletedAt)
        };
        foreach (var entry in RedisHashExtensions.ToHashEntries(job))
        {
            arguments.Add(entry.Name);
            arguments.Add(entry.Value);
        }

        cancellationToken.ThrowIfCancellationRequested();
        // Await issued mutations even if the caller is canceled. All keys must share a slot on Redis Cluster.
        var result = (long)await _database.ScriptEvaluateAsync(SaveTerminalScript,
            [jobKey, _cleanupKey, GetReceiptKey(claim.ReceiptId)], arguments.ToArray());
        if (result == -2)
            throw new InvalidOperationException($"Cannot finalize job '{job.Id}': receipt '{claim.ReceiptId}' is missing or does not match the job.");
        if (result < 0)
            throw new InvalidOperationException($"Cannot finalize missing job '{job.Id}' or overwrite a different terminal state.");
    }

    public async Task<bool> HeartbeatAsync(ConversionJobReceipt claim, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claim);
        cancellationToken.ThrowIfCancellationRequested();
        // Do not abandon an issued write; the script never recreates an absent receipt.
        var result = (long)await _database.ScriptEvaluateAsync(HeartbeatScript,
            [GetReceiptKey(claim.ReceiptId)],
            [claim.JobId.ToString("N"), DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()]);
        return result == 1;
    }

    public async Task AcknowledgeAsync(ConversionJobReceipt claim, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Repeated ACKs are harmless; mismatched receipts must not be removed.
        await _database.ScriptEvaluateAsync(AcknowledgeScript, [GetReceiptKey(claim.ReceiptId)],
            [claim.JobId.ToString("N")]);
    }

    public async Task<ConversionJobReceipt> ClaimAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var receiptId = Guid.NewGuid();
            // Store UTC Unix milliseconds and return the same timestamp to the worker.
            var heartbeatMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var receiptKey = GetReceiptKey(receiptId);
            if (receiptKey == _queueKey || receiptKey == _cleanupKey)
                throw new InvalidOperationException("Receipt key must be different from queue and cleanup keys.");
            // Await an issued mutation even on cancellation, so an acquired delivery
            // either reaches the worker or remains recorded in processing.
            var value = await _database.ScriptEvaluateAsync(
                ClaimScript, [_queueKey, receiptKey], [heartbeatMilliseconds]);
            if (!value.IsNull)
            {
                if (Guid.TryParseExact(value.ToString(), "N", out var jobId))
                    return new ConversionJobReceipt(jobId, receiptId,
                        DateTimeOffset.FromUnixTimeMilliseconds(heartbeatMilliseconds));

                throw new InvalidOperationException(
                    $"Invalid job ID retained in '{receiptKey}'.");
            }

            await Task.Delay(_pollInterval, cancellationToken);
        }
    }

    private RedisKey GetReceiptKey(Guid receiptId) => $"{_receiptKey}:{receiptId:N}";

    public async Task SubmitAsync(ConversionJob job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        cancellationToken.ThrowIfCancellationRequested();
        if (job.Id == Guid.Empty || job.Status != ConversionJobStatus.Pending)
            throw new ArgumentException("Only pending jobs with non-empty IDs can be submitted.", nameof(job));

        // Match the repository's existing metadata key format.
        RedisKey jobKey = $"{_jobKeyPrefix}:{job.Id}";
        if (jobKey == _queueKey)
            throw new InvalidOperationException("Job and queue keys must be different.");

        var submission = JsonSerializer.Serialize(new
        {
            job.Id, job.OriginalFileName, job.ContentType, job.InputStorageKey, job.CreatedAt
        });
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(submission)));
        var arguments = new List<RedisValue> { job.Id.ToString("N"), fingerprint };
        foreach (var entry in RedisHashExtensions.ToHashEntries(job))
        {
            arguments.Add(entry.Name);
            arguments.Add(entry.Value);
        }

        cancellationToken.ThrowIfCancellationRequested();
        // No WaitAsync(token): do not abandon an issued write on cancellation.
        // On transport failure the outcome is unknown; callers must retain input.
        // Redis Cluster requires both keys to share a hash slot.
        var result = (long)await _database.ScriptEvaluateAsync(
            SubmitScript, [jobKey, _queueKey], arguments.ToArray());
        if (result == -1)
            throw new InvalidOperationException($"Job '{job.Id}' was already submitted with different data.");
        if (result == -2)
            throw new InvalidOperationException($"Job '{job.Id}' has metadata without submission evidence; reconciliation is required.");
    }
}
