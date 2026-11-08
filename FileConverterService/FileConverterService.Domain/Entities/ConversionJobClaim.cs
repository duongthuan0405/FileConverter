namespace FileConverterService.Domain.Entities;

/// <summary>Receipt identifies one delivery; ACK must match both receipt and job.</summary>
public sealed record ConversionJobClaim(
    Guid JobId,
    Guid ReceiptId,
    DateTimeOffset LastHeartbeatAt = default);
