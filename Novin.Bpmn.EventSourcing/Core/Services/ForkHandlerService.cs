using Novin.Bpmn.EventSourcing.Core.Executions;
using Novin.Bpmn.EventSourcing.Core.Services;
using Novin.Bpmn.EventSourcing.Events;
using Novin.Bpmn.EventSourcing.Feel;
using ExecutionContext = Novin.Bpmn.EventSourcing.Core.Executions.ExecutionContext;


public class ForkHandlerService : IForkHandlerService
{
    public List<ExecutionContext> PrepareForks(
        ExecutionContext sourceContext,
        ElementCompleted @event,
        FlowTopology topology,
        List<string> targets)
    {
        var forks = new List<ExecutionContext>();

        foreach (var targetId in targets)
        {
            var flow = topology.SequenceFlows.Values
                .FirstOrDefault(f => f.SourceRef == @event.ElementId && f.TargetRef == targetId);
            if (flow == null) continue;

            var fork = sourceContext.Clone();
            fork.MoveToNext(targetId);

            bool conditionSatisfied = EvaluateCondition(flow.ConditionExpression, sourceContext.LocalVariables);
            if (!conditionSatisfied)
            {
                fork.State = ExecutionState.DeActive;
                fork.IsExecutable = false;
            }
            
            foreach (var kv in flow.Metadata)
                fork.LocalVariables[kv.Key] = kv.Value;

            forks.Add(fork);
        }

        return forks;
    }

    private bool EvaluateCondition(string? expression, Dictionary<string, object?> variables)
    {
        if (string.IsNullOrWhiteSpace(expression)) return true;

        try
        {
            return FeelEngine.Evaluate<bool>(expression, variables);
        }
        catch
        {
            return false;
        }
    }
}