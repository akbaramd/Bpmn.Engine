using Novin.Bpmn.Test.Core;
using Novin.Bpmn.Test.Executors.Abstracts;
using Novin.Bpmn.Test.Models;

namespace Novin.Bpmn.Test.Executors;

public class ExclusiveGatewayExecutor : IGatewayExecutor
{
    private readonly ScriptHandler _scriptHandler;

    public ExclusiveGatewayExecutor()
    {
        _scriptHandler = new ScriptHandler();
    }

    public BpmnFlowElement? Execute(BpmnFlowElement element, BpmnInstance? context)
    {
        if (element is BpmnExclusiveGateway gateway)
        {
            Console.WriteLine($"Executing Exclusive Gateway: {gateway.name}");

            var outgoingFlows = FindOutgoingFlows(gateway.id, context.BpmnDefinitions);
            foreach (var flow in outgoingFlows)
            {
                if (flow.conditionExpression != null && EvaluateCondition(flow.conditionExpression, context).Result)
                {
                    return flow;
                }
            }

            var defaultFlow = outgoingFlows.FirstOrDefault(f => f.conditionExpression == null);
            if (defaultFlow != null)
            {
                return defaultFlow;
            }
        }

        return null;
    }

    private List<BpmnSequenceFlow> FindOutgoingFlows(string sourceRef, BpmnDefinitions? definitions)
    {
        var flows = new List<BpmnSequenceFlow>();
        foreach (var item in definitions.Items)
            if (item is BpmnProcess process)
                foreach (var element in process.Items)
                    if (element is BpmnSequenceFlow flow && flow.sourceRef == sourceRef)
                        flows.Add(flow);
        return flows;
    }

    private async Task<bool> EvaluateCondition(BpmnExpression conditionExpression, BpmnInstance context)
    {
        try
        {
            var expression = string.Join(" ", conditionExpression.Text);
            var globals = new ScriptGlobals { Instance = context };
            return await _scriptHandler.EvaluateConditionAsync(expression, globals);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error evaluating condition: {ex.Message}");
            return false;
        }
    }
}