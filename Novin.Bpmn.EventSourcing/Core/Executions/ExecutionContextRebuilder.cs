using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Events;

namespace Novin.Bpmn.EventSourcing.Core.Executions;

public class ExecutionContextRebuilder : IExecutionContextRebuilder
{
    public ExecutionContext Rebuild(Guid instanceId, IEnumerable<IBpmnEvent> events)
    {
        var context = new ExecutionContext { InstanceId = instanceId };
        foreach (var e in events.OrderBy(e => e.Timestamp))
        {
            Apply(context, e);
        }
        return context;
    }

    private void Apply(ExecutionContext context, IBpmnEvent @event)
    {
        switch (@event)
        {
            case ElementProcessing ep:
                context.CurrentElementId = ep.ElementId;
                context.State = ExecutionState.Active;
                context.Version++;
                break;
            case ElementCompleted ec:
                context.CurrentElementId = ec.ElementId;
                context.State = ExecutionState.Completed;
                context.Version++;
                break;
            case ElementFailed ef:
                context.State = ExecutionState.Failed;
                break;
        }
    }
}