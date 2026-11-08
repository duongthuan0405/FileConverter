namespace FileConverterService.Infrastructure.Options;

public sealed class CleanupOptions
{
    public int RetentionMinutes { get; set; } = 10;
    public int IntervalSeconds { get; set; } = 30;
    public int BatchSize { get; set; } = 100;
}
