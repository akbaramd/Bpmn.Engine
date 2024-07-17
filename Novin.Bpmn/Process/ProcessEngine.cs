using System.Dynamic;
using Novin.Bpmn.Test;
using Novin.Bpmn.Test.Core;
using Novin.Bpmn.Test.Executors;
using Novin.Bpmn.Test.Executors.Abstracts;
using Novin.Bpmn.Test.Models;

public class ProcessEngine
{
    private readonly BpmnDefinitionsHandler definitionsHandler;
    private readonly ScriptHandler scriptHandler;
    private readonly IExecutor scriptExecuter;
    public ProcessInstance Instance { get; set; }

    public ProcessEngine(string path)
    {
        scriptHandler = new ScriptHandler();
        scriptExecuter = new ScriptTaskExecutor();
        var deserializer = new BpmnFileDeserializer();
        var defination = deserializer.Deserialize(path);
        definitionsHandler = new BpmnDefinitionsHandler(defination);
        Instance = new ProcessInstance
        {
            Id = Guid.NewGuid().ToString(),
            Definition = defination,
            Variables = new ExpandoObject(),
            ActiveTasks = new List<BpmnFlowElement>()
            {
                definitionsHandler.GetFirstStartEvent()
            }
        };
    }


    public async Task StartProcess()
    {
        while (Instance.ActiveTasks.Any())
        {
            foreach (var element in Instance.ActiveTasks.ToList())
            {
                await StartProcess(element);
            }
        }
    }

    private async Task StartProcess(BpmnFlowElement node)
    {
       

        switch (node)
        {
            case BpmnScriptTask scriptTask:
                await scriptExecuter.ExecuteAsync(scriptTask, this);
                break;
            case BpmnInclusiveGateway inclusiveGateway:
                if (!CheckForInclusiveMerge(inclusiveGateway))
                {
                    Instance.ActiveTasks.Remove(node);
                    return;
                }

                ;
                break;
            case BpmnParallelGateway parallelGateway:
                if (!CheckForParallelMerge(parallelGateway))
                {
                    Instance.ActiveTasks.Remove(node);
                    return;
                }

                ;
                break;
        }

        var findNextRoutes = await FindNextRoutes(node);


        Instance.ActiveTasks.AddRange(findNextRoutes);
        Instance.ActiveTasks.Remove(node);
    }


    private async Task<List<BpmnFlowElement>> FindNextRoutes(BpmnFlowElement node)
    {
        var routes = new List<BpmnFlowElement>();

        if (node is BpmnGateway)
        {
            switch (node)
            {
                case BpmnExclusiveGateway exclusiveGateway:
                    routes.AddRange(await FindNextExclusiveRoutes(exclusiveGateway));
                    break;
                case BpmnInclusiveGateway inclusiveGateway:
                    routes.AddRange(await FindNextInclusiveRoutes(inclusiveGateway));
                    break;
                case BpmnParallelGateway parallelGateway:
                    routes.AddRange(await FindNextParallelRoutes(parallelGateway));
                    break;
            }
        }
        else
        {
            var outgoing = definitionsHandler.GetOutgoingSequenceFlows(node);
            foreach (var flow in outgoing)
            {
                routes.Add(definitionsHandler.GetElementById(flow.targetRef));
            }
        }


        return routes;
    }

    private async Task<List<BpmnFlowElement>> FindNextExclusiveRoutes(BpmnExclusiveGateway node)
    {
        var routes = new List<BpmnFlowElement>();

        var outgoing = definitionsHandler.GetOutgoingSequenceFlows(node);

        foreach (var flow in outgoing)
        {
            var globals = new { Instance = Instance };
            if (flow.conditionExpression is not null)
            {
                var expression = string.Join(" ", flow.conditionExpression.Text);
                if (!await scriptHandler.EvaluateConditionAsync(expression, globals)) continue;
                ;
            }

            routes.Add(definitionsHandler.GetElementById(flow.targetRef));
            break;
        }


        return routes;
    }

    private async Task<List<BpmnFlowElement>> FindNextInclusiveRoutes(BpmnInclusiveGateway node)
    {
        var routes = new List<BpmnFlowElement>();

        var outgoing = definitionsHandler.GetOutgoingSequenceFlows(node);

        foreach (var flow in outgoing)
        {
            var globals = new ScriptGlobals() { Instance = Instance };
            if (flow.conditionExpression is not null)
            {
                var expression = string.Join(" ", flow.conditionExpression.Text);
                if (!await scriptHandler.EvaluateConditionAsync(expression, globals)) continue;
                ;
            }

            var element = definitionsHandler.GetElementById(flow.targetRef);
            routes.Add(element);
        }


        return routes;
    }

    private bool CheckForParallelMerge(BpmnParallelGateway parallelGateway)
    {
        if (!Instance.GatewayState.ContainsKey(parallelGateway.id))
        {
            Instance.GatewayState[parallelGateway.id] = 0;
        }

        Instance.GatewayState[parallelGateway.id]++;

        if (Instance.GatewayState[parallelGateway.id] >= parallelGateway.incoming.Length)
        {
            return true;
        }

        return false;
    }

    private void TraverseBackToPreviousGateways(BpmnFlowElement element, HashSet<BpmnGateway> gateways)
    {
        var incomingFlows = definitionsHandler.GetIncomingSequenceFlows(element);

        foreach (var flow in incomingFlows)
        {
            var sourceElement = definitionsHandler.GetElementById(flow.sourceRef);
            if (sourceElement is BpmnGateway gateway)
            {
                gateways.Add(gateway);
            }
            else
            {
                TraverseBackToPreviousGateways(sourceElement, gateways);
            }
        }
    }


    private void AssignForkId(BpmnFlowElement element, string forkId)
    {
        if (!Instance.ForkIds.ContainsKey(element.id))
        {
            Instance.ForkIds[element.id] = new HashSet<string>();
        }

        Instance.ForkIds[element.id].Add(forkId);

        var outgoingFlows = definitionsHandler.GetOutgoingSequenceFlows(element);
        foreach (var flow in outgoingFlows)
        {
            var targetElement = definitionsHandler.GetElementById(flow.targetRef);

            // Continue assigning fork IDs until the next gateway is reached
            if (!(targetElement is BpmnGateway))
            {
                AssignForkId(targetElement, forkId);
            }
            else
            {
                if (!Instance.ForkIds.ContainsKey(targetElement.id))
                {
                    Instance.ForkIds[targetElement.id] = new HashSet<string>();
                }

                Instance.ForkIds[targetElement.id].Add(forkId);
            }
        }
    }


    private bool CheckForInclusiveMerge(BpmnInclusiveGateway inclusiveGateway)
    {
        // Check if the gateway state already contains an entry for this inclusive gateway
        if (!Instance.GatewayState.ContainsKey(inclusiveGateway.id))
        {
            // Initialize the counter for this inclusive gateway
            Instance.GatewayState[inclusiveGateway.id] = 0;
        }

        // Increment the counter for the number of times this inclusive gateway has been encountered
        Instance.GatewayState[inclusiveGateway.id]++;

        // Get the fork IDs from the incoming flows
        var incomingFlows = definitionsHandler.GetIncomingSequenceFlows(inclusiveGateway);
        var forkIds = new HashSet<string>();

        foreach (var flow in incomingFlows)
        {
            if (Instance.ForkIds.ContainsKey(flow.sourceRef))
            {
                forkIds.UnionWith(Instance.ForkIds[flow.sourceRef]);
            }
        }

        if (Instance.GatewayState[inclusiveGateway.id] >= forkIds.Count)
        {
            return true;
        }

        return false;
    }

    private bool IsForkCompleted(string forkId)
    {
        foreach (var key in Instance.GatewayState.Keys)
        {
            if (Instance.ForkIds.ContainsKey(key) && Instance.ForkIds[key].Contains(forkId))
            {
                if (Instance.GatewayState[key] == 0)
                {
                    return false;
                }
            }
        }

        return true;
    }


    private async Task<List<BpmnFlowElement>> FindNextParallelRoutes(BpmnParallelGateway node)
    {
        var outgoing = definitionsHandler.GetOutgoingSequenceFlows(node);
        return outgoing.Select(flow => definitionsHandler.GetElementById(flow.targetRef)).ToList();
    }
}