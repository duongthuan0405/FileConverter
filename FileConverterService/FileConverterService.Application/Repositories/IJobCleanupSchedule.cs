namespace FileConverterService.Application.Repositories;

public interface IJobCleanupSchedule
{
    /// <summary>Reads up to count due job IDs, earliest first. Does not claim or remove them.</summary>
    Task<IReadOnlyList<Guid>> GetDueJobIdsAsync(DateTimeOffset now, int count, CancellationToken cancellationToken = default);
    /// <summary>Deletes terminal metadata and its schedule together after files have been removed.</summary>
    Task DeleteJobAsync(Guid jobId, CancellationToken cancellationToken = default);
    /// <summary>Postpones an existing member only; never recreates a deleted schedule or shortens its due time.</summary>
    Task DeferAsync(Guid jobId, DateTimeOffset retryAt, CancellationToken cancellationToken = default);
}
