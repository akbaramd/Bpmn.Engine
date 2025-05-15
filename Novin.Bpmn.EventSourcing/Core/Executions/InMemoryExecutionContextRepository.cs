namespace Novin.Bpmn.EventSourcing.Core.Executions;

public class InMemoryExecutionContextRepository : IExecutionContextRepository
{
    private readonly Dictionary<Guid, ExecutionContext> _store = new();

    public ExecutionContext? Get(Guid contextId) =>
        _store.TryGetValue(contextId, out var ctx) ? ctx : null;

    public void Save(ExecutionContext context) => _store[context.ContextId] = context;

    public void Remove(Guid contextId) => _store.Remove(contextId);

    public IReadOnlyList<ExecutionContext> GetByInstanceId(Guid instanceId) =>
        _store.Values.Where(x => x.InstanceId == instanceId).ToList();
}