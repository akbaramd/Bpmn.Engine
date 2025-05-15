using Novin.Bpmn.EventSourcing.Contracts;

namespace Novin.Bpmn.EventSourcing.Core.Executions;

public interface IExecutionContextRebuilder
{
    ExecutionContext Rebuild(Guid instanceId, IEnumerable<IBpmnEvent> events);
}