using Novin.Bpmn.Test.Executors.Abstracts;
using Novin.Bpmn.Test.Models;
using Novin.Bpmn.Test.Core;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace Novin.Bpmn.Test.Executors
{
    public class ExclusiveGatewayExecutor : IGatewayExecutor
    {
        private readonly ScriptHandler _scriptHandler = new();

        public async Task<List<string>?> ExecuteAsync(BpmnFlowElement element, BpmnEngine engine)
        {
            if (element is BpmnExclusiveGateway gateway)
            {
                var flows = engine.Instance.BpmnDefinitions.Items.OfType<BpmnProcess>()
                    .SelectMany(process => process.Items.OfType<BpmnSequenceFlow>())
                    .Where(flow => flow.sourceRef == gateway.id);

                foreach (var flow in flows)
                {
                    if (await EvaluateCondition(flow.conditionExpression, engine))
                    {
                        return new List<string> { flow.targetRef };
                    }
                }
            }

            return null;
        }

        private async Task<bool> EvaluateCondition(BpmnExpression conditionExpression, BpmnEngine engine)
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