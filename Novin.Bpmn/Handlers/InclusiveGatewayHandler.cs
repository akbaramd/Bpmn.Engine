using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Novin.Bpmn.Test.Abstractions;
using Novin.Bpmn.Test.Core;
using Novin.Bpmn.Test.Models;

namespace Novin.Bpmn.Test.Handlers
{
    public class InclusiveGatewayHandler : IGatewayHandler
    {
        public async Task HandleGateway(BpmnNode node, BpmnEngine engine)
        {
            if (!CheckForInclusiveMerge(node))
            {
                return;
            }

            var currentInstance = node.Instances.Peek();
            var isExecutable = currentInstance.Merges.Any(x => x == currentInstance.Tokens.FirstOrDefault());
            currentInstance.Merges.Clear();
            var outgoingTasks = node.OutgoingFlows.Select(async flow =>
            {
                var globals = new ScriptGlobals { State = engine.State };
                if (!string.IsNullOrWhiteSpace(flow.conditionExpression?.Text.ToString()))
                {
                    var expression = string.Join(" ", flow.conditionExpression.Text);
                    if (!await engine.ScriptHandler.EvaluateConditionAsync(expression, globals))
                    {
                        await CreateAndStartNode(engine, node, flow, Guid.NewGuid().ToString(), false);
                        return;
                    }
                }
                await CreateAndStartNode(engine, node, flow, Guid.NewGuid().ToString(), isExecutable);
            });
            await Task.WhenAll(outgoingTasks);

            currentInstance.IsExpired = true;
        }

        public bool CheckForInclusiveMerge(BpmnNode node)
        {
            var currentInstance = node.Instances.Peek();
            if (!currentInstance.Merges.Any())
                currentInstance.Merges = new Stack<string>();

            currentInstance.Merges.Push(currentInstance.Tokens.FirstOrDefault());
            return currentInstance.Merges.Count == node.IncomingFlows.Count;
        }

        private async Task CreateAndStartNode(BpmnEngine engine, BpmnNode node, BpmnSequenceFlow flow, string token, bool isExecutable)
        {
            var element = engine.DefinitionsHandler.GetElementById(flow.targetRef);
            var newNode = engine.CreateNewNode(element, token, isExecutable, node.Instances.Peek().Tokens.First());
            engine.State.ActiveNodes.Add(newNode);

            // Add outgoing transition
            node.Instances.Peek().AddTransition(node.Instances.Peek().Tokens.First(), token, DateTime.Now, false);

            await engine.StartProcess(newNode);
        }
    }
}
