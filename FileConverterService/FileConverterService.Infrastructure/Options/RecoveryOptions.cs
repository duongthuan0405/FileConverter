namespace FileConverterService.Infrastructure.Options;

public sealed class RecoveryOptions
{
    public int HeartbeatTimeoutSeconds { get; set; } = 60;
    public int IntervalSeconds { get; set; } = 30;
    public int ScanPageSize { get; set; } = 100;
}
