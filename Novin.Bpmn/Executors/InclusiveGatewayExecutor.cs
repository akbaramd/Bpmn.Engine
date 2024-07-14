using Novin.Bpmn.Test.Core;
using Novin.Bpmn.Test.Executors.Abstracts;
using Novin.Bpmn.Test.Models;

namespace Novin.Bpmn.Test.Executors;

public class InclusiveGatewayExecutor : IGatewayExecutor
{
    private readonly ScriptHandler _scriptHandler;

    public InclusiveGatewayExecutor()
    {
        _scriptHandler = new ScriptHandler();
    }

    public BpmnFlowElement? Execute(BpmnFlowElement element, BpmnInstance context)
    {
        if (element is BpmnInclusiveGateway gateway)
        {
            Console.WriteLine($"Executing Inclusive Gateway: {gateway.name}");
            var outgoingFlows = FindOutgoingFlows(gateway.id, context.BpmnDefinitions);
            foreach (var flow in outgoingFlows)
            {
                if (flow.conditionExpression == null || EvaluateCondition(flow.conditionExpression, context).Result)
                {
                    return flow;
                }
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
