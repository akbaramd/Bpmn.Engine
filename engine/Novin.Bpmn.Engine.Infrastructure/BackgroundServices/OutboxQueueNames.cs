namespace Novin.Bpmn.Engine.Infrastructure.BackgroundServices;

public static class OutboxQueueNames
{
    public const string Prefix = "novin-outbox";
    public static string PartitionQueue(int p) => $"{Prefix}-p{p}";
}