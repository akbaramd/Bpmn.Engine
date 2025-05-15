public class InMemoryFlowTopologyStore : IFlowTopologyStore
{
    // کلید ذخیره‌سازی: (DeploymentId, ProcessId)
    private readonly Dictionary<(Guid DeploymentId, string ProcessId), FlowTopology> _storage = new();

    public void Save(FlowTopology topology)
    {
        if (topology == null)
            throw new ArgumentNullException(nameof(topology));

        var key = (topology.DeploymentId, topology.ProcessId);
        _storage[key] = topology;
    }

    public FlowTopology? Get(Guid deploymentId, string processId)
    {
        var key = (deploymentId, processId);
        return _storage.TryGetValue(key, out var topology) ? topology : null;
    }

    public IReadOnlyList<FlowTopology> GetAllByDeployment(Guid deploymentId)
    {
        return _storage
            .Where(kv => kv.Key.DeploymentId == deploymentId)
            .Select(kv => kv.Value)
            .ToList();
    }

    public bool Exists(Guid deploymentId, string processId)
    {
        var key = (deploymentId, processId);
        return _storage.ContainsKey(key);
    }
}