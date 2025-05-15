public class FlowTopology
{
    public Guid TopologyId { get; init; } = Guid.NewGuid();

    public Guid DeploymentId { get; init; }

    public string ProcessId { get; init; } = default!;
    public string? ProcessName { get; init; }

    public Dictionary<string, FlowNode> Nodes { get; init; } = new();

    public Dictionary<string, List<string>> Outgoing { get; init; } = new();
    public Dictionary<string, List<string>> Incoming { get; init; } = new();

    // متدهای کمکی برای تحلیل توپولوژی

    public IEnumerable<FlowNode> GetStartEvents() =>
        Nodes.Values.Where(n => n.IsStartEvent);

    public IEnumerable<FlowNode> GetEndEvents() =>
        Nodes.Values.Where(n => n.IsEndEvent);

    public IEnumerable<FlowNode> GetGateways() =>
        Nodes.Values.Where(n => n.IsGateway);

    public IEnumerable<string> GetNextNodes(string elementId) =>
        Outgoing.TryGetValue(elementId, out var next) ? next : Enumerable.Empty<string>();

    public IEnumerable<string> GetPreviousNodes(string elementId) =>
        Incoming.TryGetValue(elementId, out var prev) ? prev : Enumerable.Empty<string>();
}