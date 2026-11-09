namespace FileConverterService.Domain.Entities;

/// <summary>
/// Identifies one delivery of a job so heartbeat, state changes and ACK can verify ownership.
/// </summary>
public sealed class ConversionJobReceipt
{
    public Guid JobId { get; }
    public Guid ReceiptId { get; }
    public DateTimeOffset LastHeartbeatAt { get; }

    public ConversionJobReceipt(Guid jobId, Guid receiptId, DateTimeOffset lastHeartbeatAt = default)
    {
        JobId = jobId;
        ReceiptId = receiptId;
        LastHeartbeatAt = lastHeartbeatAt;
    }
}
