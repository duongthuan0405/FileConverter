namespace FileConverterService.Application.Queues;

/// <summary>Receipt identifies one delivery; ACK must match both receipt and job.</summary>
public sealed record ConversionJobClaim(Guid JobId, Guid ReceiptId);
