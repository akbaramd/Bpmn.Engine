using System.Dynamic;
using Novin.Bpmn.Test;
using Novin.Bpmn.Test.Core;
using Novin.Bpmn.Test.Executors;
using Novin.Bpmn.Test.Executors.Abstracts;
using Novin.Bpmn.Test.Models;
using Novin.Bpmn.Test.Process;

public class ProcessEngine
{
    private readonly BpmnDefinitionsHandler definitionsHandler;
    private readonly IExecutor scriptExecuter;
    private readonly ScriptHandler scriptHandler;

    public ProcessEngine(string path)
    {
        scriptHandler = new ScriptHandler();
        scriptExecuter = new ScriptTaskExecutor();
        var deserializer = new BpmnFileDeserializer();
        var definition = deserializer.Deserialize(path);
        definitionsHandler = new BpmnDefinitionsHandler(definition);
        Instance = new ProcessInstance
        {
            Id = Guid.NewGuid().ToString(),
            Definition = definition,
            Variables = new ExpandoObject(),
            ActiveNode = new List<ProcessNode>
            {
                ConvertElementToNode(definitionsHandler.GetFirstStartEvent())
            }
        };
    }

    public ProcessInstance Instance { get; set; }

    private ProcessNode ConvertElementToNode(BpmnFlowElement element, string? token = null)
    {
        if (string.IsNullOrWhiteSpace(token)) token = Guid.NewGuid().ToString();
        return new ProcessNode
        {
            Id = element.id,
            Token = token,
            Element = element
        };
    }

    public async Task StartProcess()
    {
        if (Instance.ActiveNode.Any())
            foreach (var element in Instance.ActiveNode.ToList())
                await StartProcess(element);
    }

    private async Task StartProcess(ProcessNode node)
    {
        switch (node.Element)
        {
            case BpmnScriptTask scriptTask:
                await scriptExecuter.ExecuteAsync(scriptTask, this);
                break;
            case BpmnInclusiveGateway inclusiveGateway:
                if (!CheckForInclusiveMerge(inclusiveGateway, node))
                {
                    Instance.ActiveNode.Remove(node);
                    return;
                }

                break;
            case BpmnParallelGateway parallelGateway:
                if (!CheckForParallelMerge(parallelGateway, node))
                {
                    Instance.ActiveNode.Remove(node);
                    return;
                }

                break;
        }

        var findNextRoutes = await FindNextNodes(node);
        Instance.ActiveNode.Remove(node);

        foreach (var nextRoute in findNextRoutes)
        {
            Instance.ActiveNode.Add(nextRoute);
            await StartProcess(nextRoute);
        }
    }

    private async Task<List<ProcessNode>> FindNextNodes(ProcessNode node)
    {
        var routes = new List<ProcessNode>();

        if (node.Element is BpmnGateway)
        {
            switch (node.Element)
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
            var outgoing = definitionsHandler.GetOutgoingSequenceFlows(node.Element);
            foreach (var flow in outgoing)
                routes.Add(ConvertElementToNode(definitionsHandler.GetElementById(flow.targetRef), node.Token));
        }

        return routes;
    }

    private async Task<List<ProcessNode>> FindNextParallelRoutes(BpmnParallelGateway node)
    {
        var routes = new List<ProcessNode>();
        var outgoing = definitionsHandler.GetOutgoingSequenceFlows(node);
        foreach (var flow in outgoing)
        {
            var token = Guid.NewGuid().ToString();
            routes.Add(ConvertElementToNode(definitionsHandler.GetElementById(flow.targetRef), token));
            if (!Instance.GatewayForkState.ContainsKey(node.id))
                Instance.GatewayMergeState[node.id] = new HashSet<string>();

            Instance.GatewayMergeState[node.id].Add(token);
        }

        return routes;
    }

    private async Task<List<ProcessNode>> FindNextExclusiveRoutes(BpmnExclusiveGateway node)
    {
        var routes = new List<ProcessNode>();
        var outgoing = definitionsHandler.GetOutgoingSequenceFlows(node);

        foreach (var flow in outgoing)
        {
            var globals = new ScriptGlobals { Instance = Instance };
            if (flow.conditionExpression is not null)
            {
                var expression = string.Join(" ", flow.conditionExpression.Text);
                if (!await scriptHandler.EvaluateConditionAsync(expression, globals)) continue;
            }

            var token = Guid.NewGuid().ToString();
            routes.Add(ConvertElementToNode(definitionsHandler.GetElementById(flow.targetRef), token));

            if (!Instance.GatewayForkState.ContainsKey(node.id))
                Instance.GatewayMergeState[node.id] = new HashSet<string>();

            Instance.GatewayMergeState[node.id].Add(token);
            break;
        }

        return routes;
    }

    private async Task<List<ProcessNode>> FindNextInclusiveRoutes(BpmnInclusiveGateway node)
    {
        var routes = new List<ProcessNode>();
        var outgoing = definitionsHandler.GetOutgoingSequenceFlows(node);

        foreach (var flow in outgoing)
        {
            var globals = new ScriptGlobals { Instance = Instance };
            if (flow.conditionExpression is not null)
            {
                var expression = string.Join(" ", flow.conditionExpression.Text);
                if (!await scriptHandler.EvaluateConditionAsync(expression, globals)) continue;
            }

            var element = definitionsHandler.GetElementById(flow.targetRef);
            var token = Guid.NewGuid().ToString();
            routes.Add(ConvertElementToNode(element, token));

            if (!Instance.GatewayForkState.ContainsKey(node.id))
                Instance.GatewayForkState[node.id] = new HashSet<string>();

            Instance.GatewayForkState[node.id].Add(token);
        }

        return routes;
    }

    private bool CheckForParallelMerge(BpmnParallelGateway parallelGateway, ProcessNode node)
    {
        if (!Instance.GatewayMergeState.ContainsKey(parallelGateway.id))
            Instance.GatewayMergeState[parallelGateway.id] = new HashSet<string>();

        Instance.GatewayMergeState[parallelGateway.id].Add(node.Token);

        if (Instance.GatewayMergeState[parallelGateway.id].Count >= parallelGateway.incoming.Length) return true;

        return false;
    }

    private bool CheckForInclusiveMerge(BpmnInclusiveGateway inclusiveGateway, ProcessNode node)
    {
        if (!Instance.GatewayMergeState.ContainsKey(inclusiveGateway.id))
            Instance.GatewayMergeState[inclusiveGateway.id] = new HashSet<string>();

        Instance.GatewayMergeState[inclusiveGateway.id].Add(node.Token);

        var incomingFlows = definitionsHandler.GetIncomingSequenceFlows(inclusiveGateway);
        var previousGateway = new HashSet<BpmnGateway>();

        foreach (var flow in incomingFlows)
            TraverseBackToPreviousGateways(definitionsHandler.GetElementById(flow.sourceRef), previousGateway);

        if (previousGateway.Any())
        {
            var tokens = previousGateway.SelectMany(x => Instance.GatewayForkState[x.id]);

            if (Instance.GatewayMergeState[inclusiveGateway.id].Count >= tokens.Count()) return true;
        }
        else
        {
            return true;
        }

        return false;
    }
    
    private void TraverseBackToPreviousGateways(BpmnFlowElement element, HashSet<BpmnGateway> tokens)
    {
        var incomingFlows = definitionsHandler.GetIncomingSequenceFlows(element);

        foreach (var flow in incomingFlows)
        {
            var sourceElement = definitionsHandler.GetElementById(flow.sourceRef);
            if (sourceElement is BpmnGateway bpmnGateway)
            {
                if (Instance.GatewayForkState.ContainsKey(sourceElement.id))
                {
                    tokens.Add(bpmnGateway);
                }
               
            }
            else
                TraverseBackToPreviousGateways(sourceElement, tokens);
        }
    }
}