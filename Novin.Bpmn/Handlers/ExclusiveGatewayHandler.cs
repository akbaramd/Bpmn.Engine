using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Core;

namespace Novin.Bpmn.Handlers
{
    public class ExclusiveGatewayHandler : IGatewayHandler
    {
        public async Task HandleGateway(BpmnNode node, BpmnEngine engine)
        {
            if (!CheckForExclusiveMerge(node))
            {
                return;
            }

    
            node.Merges.Clear();

            foreach (var flow in node.OutgoingTargets)
            {
                var globals = new ScriptGlobals { State = engine.State };
                if (flow.conditionExpression != null)
                {
                    var expression = string.Join(" ", flow.conditionExpression.Text);
                    if (await engine.ScriptHandler.EvaluateConditionAsync(expression, globals))
                    {
                        var newElement = engine.DefinitionsHandler.GetElementById(flow.targetRef);
                        var newNode = engine.CreateNewNode(newElement, node.Uid, node.IsExecutable, node.Uid);

                        // Add outgoing transition
                        node.AddTransition(node.Uid, newNode.Uid, DateTime.Now, false);
                        engine.EnqueueNode(newNode);
                        break;
                    }
                }
            }

            node.IsExpired = true;
        }

        public bool CheckForExclusiveMerge(BpmnNode node)
        {
     
            if (node.Merges.Count > 0)
                return true;

            node.Merges.Push(new Tuple<string,Guid, bool>(node.Id,node.Uid,node.IsExecutable));
            return false;
        }
    }
}