public sealed class ElasticOptions
{
    public string Url { get; init; } = "http://localhost:9200";
    public string Index { get; set; } = "bpmn-outbox";
    public int Shards { get; set; } = 1;
    public int Replicas { get; set; } = 0;

    // "1s", "500ms", etc.
    public string? RefreshInterval { get; set; } = "1s";
}
