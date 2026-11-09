namespace FileConverterService.Infrastructure.Options;

public sealed class ConversionWorkerOptions
{
    // Per application instance; each loop claims one job and finishes it before claiming again.
    public int MaxConcurrentJobs { get; set; }
    public int HeartbeatIntervalSeconds { get; set; } = 10;
}
