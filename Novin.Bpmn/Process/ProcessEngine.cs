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
    private readonly Dictionary<string, HashSet<string>> gatewaysMap = new Dictionary<string, HashSet<string>>();
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
                    routes.AddRange(await FindNextExclusiveRoutes(node));
                    break;
                case BpmnInclusiveGateway inclusiveGateway:
                    routes.AddRange(await FindNextInclusiveRoutes(node));
                    break;
                case BpmnParallelGateway parallelGateway:
                    routes.AddRange(await FindNextParallelRoutes(node));
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

    private async Task<List<ProcessNode>> FindNextParallelRoutes(ProcessNode node)
    {
        
        if (!Instance.GatewayForkState.ContainsKey(node.Id))
            Instance.GatewayForkState[node.Id] = new HashSet<string>();
        
        Instance.GatewayForkState[node.Id].Clear();

        
        var routes = new List<ProcessNode>();
        var outgoing = definitionsHandler.GetOutgoingSequenceFlows(node.Element);
        foreach (var flow in outgoing)
        {
            var token = Guid.NewGuid().ToString();
            routes.Add(ConvertElementToNode(definitionsHandler.GetElementById(flow.targetRef), token));
            Instance.GatewayForkState[node.Id].Add(token);
        }

        var gateways = new HashSet<BpmnGateway>();
        TraverseBackToPreviousGateways(node.Element, gateways);

        foreach (var gateway in gateways)
        {
            if (!gatewaysMap.ContainsKey(gateway.id))
                gatewaysMap[gateway.id] = new HashSet<string>();
            
            gatewaysMap[gateway.id].Add(node.Id);
            
            Instance.GatewayForkState[gateway.id].Remove(node.Token);
        }
        return routes;
    }

    private async Task<List<ProcessNode>> FindNextExclusiveRoutes(ProcessNode node)
    {
        
        if (!Instance.GatewayForkState.ContainsKey(node.Id))
            Instance.GatewayForkState[node.Id] = new HashSet<string>();
        
        Instance.GatewayForkState[node.Id].Clear();

        var routes = new List<ProcessNode>();
        var outgoing = definitionsHandler.GetOutgoingSequenceFlows(node.Element);

        
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

        

            Instance.GatewayForkState[node.Id].Add(token);
            break;
        }
        var gateways = new HashSet<BpmnGateway>();
        TraverseBackToPreviousGateways(node.Element, gateways);

        foreach (var gateway in gateways)
        {
            Instance.GatewayForkState[gateway.id].Remove(node.Token);
        }
        return routes;
    }

    private async Task<List<ProcessNode>> FindNextInclusiveRoutes(ProcessNode node)
    {
        var routes = new List<ProcessNode>();
        
        var outgoing = definitionsHandler.GetOutgoingSequenceFlows(node.Element);

        if (!Instance.GatewayForkState.ContainsKey(node.Id))
            Instance.GatewayForkState[node.Id] = new HashSet<string>();
        
        Instance.GatewayForkState[node.Id].Clear();
        
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

            

            Instance.GatewayForkState[node.Id].Add(token);
        }
        var gateways = new HashSet<BpmnGateway>();
        TraverseBackToPreviousGateways(node.Element, gateways);

        foreach (var gateway in gateways)
        {
            Instance.GatewayForkState[gateway.id].Remove(node.Token);
        }
        return routes;
    }

    private bool CheckForParallelMerge(BpmnParallelGateway parallelGateway, ProcessNode node)
    {
        if (!Instance.GatewayMergeState.ContainsKey(parallelGateway.id))
            Instance.GatewayMergeState[parallelGateway.id] = new HashSet<string>();

        Instance.GatewayMergeState[parallelGateway.id].Add(node.Token);

        var incomingFlows = definitionsHandler.GetIncomingSequenceFlows(parallelGateway);
        var previousGateway = new HashSet<BpmnGateway>();

        foreach (var flow in incomingFlows)
            TraverseBackToPreviousGateways(definitionsHandler.GetElementById(flow.sourceRef), previousGateway);

        if (previousGateway.Any())
        {
            var tokens = previousGateway.SelectMany(x => Instance.GatewayForkState[x.id]).ToList();
            var neededTokens = Instance.GatewayMergeState[parallelGateway.id];
            if (neededTokens.All(tokens.Contains) && neededTokens.Count == incomingFlows.Count)
            {
                Instance.GatewayMergeState.Remove(parallelGateway.id);
                foreach (var gateway in previousGateway)
                {
                    Instance.GatewayMergeState.Remove(gateway.id);
                }      
                return true;
            };
        }
        else
        {
            return true;
        }

        
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
            var tokens = previousGateway.SelectMany(x => Instance.GatewayForkState[x.id]).ToList();
            var currentTokens = Instance.GatewayMergeState[inclusiveGateway.id];
            if (currentTokens.All(tokens.Contains) && currentTokens.Count == tokens.Count)
            {
                Instance.GatewayMergeState.Remove(inclusiveGateway.id);
                foreach (var gateway in previousGateway)
                {
                    Instance.GatewayMergeState.Remove(gateway.id);
                }        
                return true;
            };
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