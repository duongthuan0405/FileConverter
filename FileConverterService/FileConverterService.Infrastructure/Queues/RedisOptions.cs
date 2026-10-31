namespace FileConverterService.Infrastructure.Queues;

public sealed class RedisOptions
{
    public string ConnectionString { get; set; } = "localhost:6379";
    public string QueueKey { get; set; } = "fileconverter:queue";
    public string JobKey { get; set; } = "fileconverter:storage:job";
    public string BatchKey { get; set; } = "fileconverter:storage:batch";

    public int PollIntervalMilliseconds { get; set; } = 250;
}
