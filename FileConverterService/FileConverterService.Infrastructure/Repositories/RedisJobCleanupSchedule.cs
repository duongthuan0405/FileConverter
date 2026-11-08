using FileConverterService.Application.Repositories;
using FileConverterService.Infrastructure.Options;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace FileConverterService.Infrastructure.Repositories;

public sealed class RedisJobCleanupSchedule(IConnectionMultiplexer connection, IOptions<RedisOptions> options)
    : IJobCleanupSchedule
{
    private const string DeferScript = """
        return redis.call('ZADD', KEYS[1], 'XX', 'GT', ARGV[1], ARGV[2])
        """;

    public async Task DeferAsync(Guid jobId, DateTimeOffset retryAt, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await connection.GetDatabase().ScriptEvaluateAsync(DeferScript,
            [(RedisKey)options.Value.CleanupKey], [retryAt.ToUnixTimeMilliseconds(), jobId.ToString("N")]);
    }

    private const string DeleteScript = """
        local status = redis.call('HGET', KEYS[1], 'Status')
        if status and status ~= '2' and status ~= '3' then
            return redis.error_reply('Cannot delete an active job')
        end
        local cleanupType = redis.call('TYPE', KEYS[2]).ok
        if cleanupType ~= 'none' and cleanupType ~= 'zset' then
            return redis.error_reply('Cleanup key must be a sorted set')
        end
        redis.call('ZREM', KEYS[2], ARGV[1])
        return redis.call('DEL', KEYS[1])
        """;

    public async Task DeleteJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await connection.GetDatabase().ScriptEvaluateAsync(DeleteScript,
            [(RedisKey)$"{options.Value.JobKey}:{jobId}", options.Value.CleanupKey], [jobId.ToString("N")]);
    }

    public async Task<IReadOnlyList<Guid>> GetDueJobIdsAsync(DateTimeOffset now, int count,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        cancellationToken.ThrowIfCancellationRequested();
        // ZRANGEBYSCORE cleanupKey -inf now LIMIT 0 count. This only reads the set.
        var members = await connection.GetDatabase().SortedSetRangeByScoreAsync(
            options.Value.CleanupKey,
            start: double.NegativeInfinity,
            stop: now.ToUnixTimeMilliseconds(),
            order: Order.Ascending,
            skip: 0,
            take: count).WaitAsync(cancellationToken);

        return members.Select(member => Guid.ParseExact(member.ToString(), "N")).ToArray();
    }
}
