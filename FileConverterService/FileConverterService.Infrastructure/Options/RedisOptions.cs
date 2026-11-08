namespace FileConverterService.Infrastructure.Options;

public sealed class RedisOptions
{
    public string ConnectionString { get; set; } = "localhost:6379";
    public string QueueKey { get; set; } = "fileconverter:queue";
    // Prefix; each delivery is a separate hash at <ReceiptKey>:<receiptIdN>.
    public string ReceiptKey { get; set; } = "fileconverter:receipt";
    public string JobKey { get; set; } = "fileconverter:storage:job";
    public string BatchKey { get; set; } = "fileconverter:storage:batch";
    public string CleanupKey { get; set; } = "fileconverter:cleanup";


    public int PollIntervalMilliseconds { get; set; } = 250;
}
