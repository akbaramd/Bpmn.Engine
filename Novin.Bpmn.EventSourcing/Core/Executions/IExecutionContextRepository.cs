namespace Novin.Bpmn.EventSourcing.Core.Executions;

public interface IExecutionContextRepository
{
    ExecutionContext? Get(Guid contextId);
    void Save(ExecutionContext context);
    void Remove(Guid contextId);
    IReadOnlyList<ExecutionContext> GetByInstanceId(Guid instanceId);
}