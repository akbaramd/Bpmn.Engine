using Novin.Bpmn.Test.Executors.Abstracts;
using Novin.Bpmn.Test.Models;
using Novin.Bpmn.Test.Core;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Novin.Bpmn.Test.Executors
{
    public class InclusiveGatewayExecutor : IGatewayExecutor
    {
        private readonly ScriptHandler _scriptHandler = new();

        public async Task<List<string>?> ExecuteAsync(BpmnFlowElement element, BpmnEngine engine)
        {
            if (element is BpmnInclusiveGateway gateway)
            {
                var flows = engine.Instance.BpmnDefinitions.Items.OfType<BpmnProcess>()
                    .SelectMany(process => process.Items.OfType<BpmnSequenceFlow>())
                    .Where(flow => flow.sourceRef == gateway.id);

                var nextNodeIds = new List<string>();
                foreach (var flow in flows)
                {
                    if (await EvaluateCondition(flow.conditionExpression, engine))
                    {
                        nextNodeIds.Add(flow.targetRef);
                    }
                }

                foreach (var activeNodeId in nextNodeIds)
                {
                    // Execute each branch asynchronously using the provided engine
                    _ = Task.Run(() => engine.ExecuteNextAsync(activeNodeId));
                }

                return null; // Inclusive gateway does not proceed immediately
            }
            return null;
        }


        private async Task<bool> EvaluateCondition(BpmnExpression? conditionExpression, BpmnEngine engine)
        {
            try
            {
                if (conditionExpression != null)
                {
                    var expression = string.Join(" ", conditionExpression.Text);
                    var globals = new ScriptGlobals { Instance = engine.Instance };
                    return await _scriptHandler.EvaluateConditionAsync(expression, globals);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error evaluating condition: {ex.Message}");
                return false;
            }

            return false;
        }
    }
}
