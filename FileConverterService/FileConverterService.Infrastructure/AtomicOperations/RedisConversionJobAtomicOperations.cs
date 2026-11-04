using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FileConverterService.Application.Jobs;
using FileConverterService.Application.Queues;
using FileConverterService.Domain.Entities;
using FileConverterService.Domain.Enums;
using FileConverterService.Infrastructure.Extensions;
using FileConverterService.Infrastructure.Queues;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace FileConverterService.Infrastructure.AtomicOperations;

public sealed class RedisConversionJobAtomicOperations : IConversionJobAtomicOperations
{
    private readonly IDatabase _database;
    private readonly string _jobKeyPrefix;
    private readonly RedisKey _queueKey;
    private readonly RedisKey _receiptKey;
    private readonly TimeSpan _pollInterval;

    private const string AcknowledgeScript = """
        if redis.call('HGET', KEYS[1], ARGV[1]) == ARGV[2] then
            return redis.call('HDEL', KEYS[1], ARGV[1])
        end
        return 0
        """;

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

    public RedisConversionJobAtomicOperations(IConnectionMultiplexer connection, IOptions<RedisOptions> options)
    {
        _database = connection.GetDatabase();
        _jobKeyPrefix = options.Value.JobKey;
        _queueKey = options.Value.QueueKey;
        _receiptKey = options.Value.ReceiptKey;
        _pollInterval = TimeSpan.FromMilliseconds(options.Value.PollIntervalMilliseconds);
    }

    public async Task AcknowledgeAsync(ConversionJobClaim claim, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Repeated ACKs are harmless; mismatched receipts must not be removed.
        await _database.ScriptEvaluateAsync(AcknowledgeScript, [_receiptKey],
            [claim.ReceiptId.ToString("N"), claim.JobId.ToString("N")]);
    }

    public async Task<ConversionJobClaim> ClaimAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var receiptId = Guid.NewGuid();
            // Await an issued mutation even on cancellation, so an acquired delivery
            // either reaches the worker or remains recorded in processing.
            var value = await _database.ScriptEvaluateAsync(
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
