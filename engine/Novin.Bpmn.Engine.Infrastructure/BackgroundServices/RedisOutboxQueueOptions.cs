
public sealed class RedisOutboxQueueOptions
{
    public string StreamPrefix { get; set; } = "novin:outbox";
    public string ConsumerGroup { get; set; } = "novin-outbox";
    public int Partitions { get; set; } = 8;

    public int ReadBatchSize { get; set; } = 100;
    public int ClaimBatchSize { get; set; } = 100;

    public int BlockMs { get; set; } = 100;           // XREADGROUP BLOCK
    public int PendingMinIdleMs { get; set; } = 1000; // XCLAIM older than this
}