namespace Novin.Bpmn.Engine.Infrastructure.BackgroundServices;

public static class OutboxPartitioning
{
    public static int PickPartition(string? key, int partitions)
    {
        key = string.IsNullOrWhiteSpace(key) ? "global" : key;

        unchecked
        {
            uint hash = 2166136261;
            for (int i = 0; i < key.Length; i++)
            {
                hash ^= key[i];
                hash *= 16777619;
            }
            return (int)(hash % (uint)partitions);
        }
    }
}