using System.Linq;
using System.Threading.Tasks;
using Novin.Bpmn.Test.Abstractions;
using Novin.Bpmn.Test.Core;

namespace Novin.Bpmn.Test.Handlers
{
    public class ExclusiveGatewayHandler : IGatewayHandler
    {
        public async Task HandleGateway(BpmnNode node, BpmnEngine engine)
        {
            if (!CheckForExclusiveMerge(node))
            {
                return;
            }

            var currentInstance = node.Instances.Peek();
            currentInstance.Merges.Clear();

            foreach (var flow in node.OutgoingFlows)
            {
                var globals = new ScriptGlobals { State = engine.State };
                if (flow.conditionExpression != null)
                {
                    var expression = string.Join(" ", flow.conditionExpression.Text);
                    if (await engine.ScriptHandler.EvaluateConditionAsync(expression, globals))
                    {
                        var newNode = engine.CreateNewNode(engine.DefinitionsHandler.GetElementById(flow.targetRef), currentInstance.Tokens.First(), currentInstance.IsExecutable, currentInstance.Tokens.First());

                        // Add outgoing transition
                        currentInstance.AddTransition(currentInstance.Tokens.First(), newNode.Instances.Peek().Tokens.First(), DateTime.Now, false);

                        engine.EnqueueNode(newNode);
                        break;
                    }
                }
            }

            currentInstance.IsExpired = true;
        }

        public bool CheckForExclusiveMerge(BpmnNode node)
        {
            var currentInstance = node.Instances.Peek();
            if (currentInstance.Merges.Count > 0)
                return true;

            currentInstance.Merges.Push(new Tuple<string, bool>(currentInstance.Tokens.FirstOrDefault(),currentInstance.IsExecutable));
            return false;
        }
    }
}