using Novin.Bpmn.EventSourcing.Events;

namespace Novin.Bpmn.EventSourcing.Core.Services;
using ExecutionContext = Novin.Bpmn.EventSourcing.Core.Executions.ExecutionContext;
public interface IForkHandlerService
{
    List<ExecutionContext> PrepareForks(
        ExecutionContext sourceContext,
        ElementCompleted @event,
        FlowTopology topology,
        List<string> targets);
}