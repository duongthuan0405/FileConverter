namespace FileConverterService.Infrastructure.Options;

public sealed class ReadinessOptions
{
    public int TimeoutSeconds { get; set; } = 5;
}
