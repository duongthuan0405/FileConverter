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

    /// <summary>Saves a terminal job and its cleanup due time together, before ACK.</summary>
    Task SaveTerminalAsync(ConversionJob job, CancellationToken cancellationToken = default);

    /// <summary>Atomically removes one pending job and records its processing receipt.</summary>
    Task<ConversionJobClaim> ClaimAsync(CancellationToken cancellationToken = default);

    /// <summary>Removes a receipt only when both receipt ID and job ID match.</summary>
    Task AcknowledgeAsync(ConversionJobClaim claim, CancellationToken cancellationToken = default);
}
