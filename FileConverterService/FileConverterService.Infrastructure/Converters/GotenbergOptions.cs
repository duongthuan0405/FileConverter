namespace FileConverterService.Infrastructure.Converters;

public sealed class GotenbergOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public int TimeoutInMinutes { get; set; } = 3;
    public int MaxAttempts { get; set; } = 4;
    public int RetryDelayInMinutes { get; set; } = 0;
}
