using FileConverterService.Domain.Entities;

namespace FileConverterService.Application.Repositories;

public interface IConversionJobAtomicOperations
{
    /// <summary>
    /// Saves metadata and enqueues together. Repeating the same submission does
    /// not enqueue again while metadata is retained. Cancellation is checked
    /// before dispatch; an issued mutation is awaited to completion.
    /// </summary>
    Task SubmitAsync(ConversionJob job, CancellationToken cancellationToken = default);

    /// <summary>Checks the receipt and stored Pending state, then persists Processing and its start time atomically.</summary>
    Task MarkProcessingAsync(ConversionJob job, ConversionJobClaim claim, CancellationToken cancellationToken = default);

    /// <summary>Checks the receipt, then saves terminal metadata and cleanup due time together, before ACK.</summary>
    Task SaveTerminalAsync(ConversionJob job, ConversionJobClaim claim, CancellationToken cancellationToken = default);

    /// <summary>Atomically removes one pending job and creates a receipt hash with JobId and initial LastHeartbeatAt.</summary>
    Task<ConversionJobClaim> ClaimAsync(CancellationToken cancellationToken = default);

    /// <summary>Updates an existing matching receipt's heartbeat; returns false if it is missing or belongs to another job.</summary>
    Task<bool> HeartbeatAsync(ConversionJobClaim claim, CancellationToken cancellationToken = default);

    /// <summary>Enumerates valid per-receipt hashes incrementally, not legacy shared receipt hashes.</summary>
    IAsyncEnumerable<ConversionJobClaim> ScanReceiptsAsync(int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Rechecks heartbeat and recovers stale receipts; marks Processing as Interrupted without retrying conversion.</summary>
    Task<bool> TryRecoverAsync(ConversionJobClaim claim, DateTimeOffset staleBefore, CancellationToken cancellationToken = default);

    /// <summary>Removes a receipt only when both receipt ID and job ID match.</summary>
    Task AcknowledgeAsync(ConversionJobClaim claim, CancellationToken cancellationToken = default);
}
