using Novin.Bpmn.Test.Executors.Abstracts;
using Novin.Bpmn.Test.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Novin.Bpmn.Test.Executors
{
    public class ParallelGatewayExecutor : IGatewayExecutor
    {
           public async Task<List<string>?> ExecuteAsync(BpmnFlowElement element, BpmnEngine engine)
        {
            if (element is BpmnParallelGateway gateway)
            {
                if (IsForkGateway(gateway.id, engine))
                {
                    return await HandleParallelForkAsync(gateway.id, engine);
                }
                else
                {
                    return await HandleParallelJoinAsync(gateway.id, engine);
                }
            }
            return null;
        }

        private bool IsForkGateway(string gatewayId, BpmnEngine engine)
        {
            return engine.Instance.BpmnDefinitions.Items.OfType<BpmnProcess>()
                .Any(process => process.Items.OfType<BpmnParallelGateway>()
                .Any(gateway => gateway.id == gatewayId &&
                                process.Items.OfType<BpmnSequenceFlow>().Count(flow => flow.sourceRef == gatewayId) > 1));
        }

        private async Task<List<string>?> HandleParallelForkAsync(string forkGatewayId, BpmnEngine engine)
        {
            var parallelFlows = engine.Instance.BpmnDefinitions.Items.OfType<BpmnProcess>()
                .SelectMany(process => process.Items.OfType<BpmnSequenceFlow>())
                .Where(flow => flow.sourceRef == forkGatewayId)
                .ToList();

            var nextNodeIds = new List<string>();
            foreach (var flow in parallelFlows)
            {
                nextNodeIds.Add(flow.targetRef);
            }

            foreach (var activeNodeId in nextNodeIds)
            {
                // Execute each branch asynchronously using the provided engine
                _ = Task.Run(() => engine.ExecuteNextAsync(activeNodeId));
            }

            return null; // Fork does not proceed immediately
        }

        private async Task<List<string>?> HandleParallelJoinAsync(string joinGatewayId, BpmnEngine engine)
        {
            var incomingFlows = engine.Instance.BpmnDefinitions.Items.OfType<BpmnProcess>()
                .SelectMany(process => process.Items.OfType<BpmnSequenceFlow>())
                .Where(flow => flow.targetRef == joinGatewayId)
                .ToList();

            if (incomingFlows.All(flow => engine.Instance.History.Contains(flow.sourceRef)))
            {
                return new List<string> { FindNextNodeId(engine, joinGatewayId) };
            }
            else
            {
                engine.Instance.ActiveNodeIds.Add(joinGatewayId); // Wait until all parallel paths reach the join gateway
                return null;
            }
        }

        private string? FindNextNodeId(BpmnEngine engine, string currentNodeId)
        {
            var nextNode = engine.Instance.BpmnDefinitions.Items.OfType<BpmnProcess>()
                .SelectMany(process => process.Items.OfType<BpmnSequenceFlow>())
                .FirstOrDefault(flow => flow.sourceRef == currentNodeId)?.targetRef;
            return nextNode;
        }
    }
}
