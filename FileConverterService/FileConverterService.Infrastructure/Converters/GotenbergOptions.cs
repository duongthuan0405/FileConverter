namespace FileConverterService.Infrastructure.Converters;

public sealed class GotenbergOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public int TimeoutInMinutes { get; set; } = 3;
    // Total budget, including attempts, retry delays, PDF download and the save callback.
    public int DeadlineInMinutes { get; set; } = 5;
    public int MaxAttempts { get; set; } = 1;
    public int RetryDelayInMinutes { get; set; } = 0;
}
