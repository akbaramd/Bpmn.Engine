using Novin.Bpmn.EventSourcing.Core.Executions;

namespace Novin.Bpmn.EventSourcing.Core.Services;

public interface IExecutionPathService
{
    ExecutionTraceMap BuildExecutionTraces(Guid instanceId);
}