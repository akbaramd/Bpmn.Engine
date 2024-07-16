using System.Xml.Serialization;
using Novin.Bpmn.Test.Core;
using Novin.Bpmn.Test.Models;

namespace Novin.Bpmn.Test.Bpmn;

public class BpmnRouteEngine
{
    public BpmnDefinitions BpmnDefinitions { get; private set; }

    public BpmnRouteEngine(string filePath)
    {
        BpmnDefinitions = DeserializeBpmnFile(filePath);
    }

    private BpmnDefinitions DeserializeBpmnFile(string filePath)
    {
        var xmlNamespaces = new XmlSerializerNamespaces();
        xmlNamespaces.Add("bpmn", "http://www.omg.org/spec/BPMN/20100524/MODEL");
        xmlNamespaces.Add("bpmndi", "http://www.omg.org/spec/BPMN/20100524/DI");
        xmlNamespaces.Add("dc", "http://www.omg.org/spec/DD/20100524/DC");
        xmlNamespaces.Add("di", "http://www.omg.org/spec/DD/20100524/DI");
        xmlNamespaces.Add("camunda", "http://camunda.org/schema/1.0/bpmn");
        xmlNamespaces.Add("xsi", "http://www.w3.org/2001/XMLSchema-instance");

        var xmlContent = File.ReadAllText(filePath);

        var serializer = new XmlSerializer(typeof(BpmnDefinitions), "http://www.omg.org/spec/BPMN/20100524/MODEL");
        using var stringReader = new StringReader(xmlContent);
        return (BpmnDefinitions)serializer.Deserialize(stringReader)!;
    }

    public async Task<List<BpmnRoute>> FindAllRoutesAsync()
    {
        var routes = new List<BpmnRoute>();
        var startNode = FindStartNode();

        if (startNode != null)
        {
            var initialRoute = new BpmnRoute(startNode.id, null);
            routes.Add(initialRoute);
            await TraverseRoutesAsync(initialRoute);
        }

        return routes;
    }

    private async Task TraverseRoutesAsync(BpmnRoute currentRoute)
    {
        var currentNodeId = currentRoute.EndNode ?? currentRoute.StartNode;
        var currentNode = FindCurrentNode(currentNodeId);

        if (currentNode != null && !(currentNode is BpmnEndEvent))
        {
            var nextNodeIds = await FindNextNodes(currentNode);

            foreach (var nextNodeId in nextNodeIds)
            {
                var newRoute = currentRoute.Fork(nextNodeId);
                currentRoute.Routes.Add(newRoute);
                await TraverseRoutesAsync(newRoute);
            }
        }
    }

    private async Task<List<string>> FindNextNodes(BpmnFlowElement currentNode)
    {
        var nextNodeIds = new List<string>();

        var flows = BpmnDefinitions.Items.OfType<BpmnProcess>()
            .SelectMany(process => process.Items.OfType<BpmnSequenceFlow>())
            .Where(flow => flow.sourceRef == currentNode.id);

        if (currentNode is BpmnGateway gateway)
        {
            var gatewayNextNodeIds = await FindNextRoute(gateway);
            nextNodeIds.AddRange(gatewayNextNodeIds);
        }
        else
        {
            foreach (var flow in flows)
            {
                nextNodeIds.Add(flow.targetRef);
            }
        }

        return nextNodeIds;
    }

    private async Task<List<string>> FindNextRoute(BpmnGateway gateway)
    {
        var nextNodeIds = new List<string>();

        switch (gateway)
        {
            case BpmnExclusiveGateway exclusiveGateway:
                nextNodeIds = await EvaluateExclusiveGateway(exclusiveGateway);
                break;
            case BpmnInclusiveGateway inclusiveGateway:
                nextNodeIds = await EvaluateInclusiveGateway(inclusiveGateway);
                break;
            case BpmnParallelGateway parallelGateway:
                nextNodeIds = await EvaluateParallelGateway(parallelGateway);
                break;
        }

        return nextNodeIds;
    }

    private async Task<List<string>> EvaluateExclusiveGateway(BpmnExclusiveGateway gateway)
    {
        var flows = BpmnDefinitions.Items.OfType<BpmnProcess>()
            .SelectMany(process => process.Items.OfType<BpmnSequenceFlow>())
            .Where(flow => flow.sourceRef == gateway.id);

        foreach (var flow in flows)
        {
            if (await EvaluateCondition(flow.conditionExpression))
            {
                return new List<string> { flow.targetRef };
            }
        }

        return new List<string>();
    }

    private async Task<List<string>> EvaluateInclusiveGateway(BpmnInclusiveGateway gateway)
    {
        var flows = BpmnDefinitions.Items.OfType<BpmnProcess>()
            .SelectMany(process => process.Items.OfType<BpmnSequenceFlow>())
            .Where(flow => flow.sourceRef == gateway.id);

        var nextNodeIds = new List<string>();
        foreach (var flow in flows)
        {
            if (await EvaluateCondition(flow.conditionExpression))
            {
                nextNodeIds.Add(flow.targetRef);
            }
        }

        return nextNodeIds;
    }

    private Task<List<string>> EvaluateParallelGateway(BpmnParallelGateway gateway)
    {
        var flows = BpmnDefinitions.Items.OfType<BpmnProcess>()
            .SelectMany(process => process.Items.OfType<BpmnSequenceFlow>())
            .Where(flow => flow.sourceRef == gateway.id);

        var nextNodeIds = flows.Select(flow => flow.targetRef).ToList();
        return Task.FromResult(nextNodeIds);
    }

    private async Task<bool> EvaluateCondition(BpmnExpression? conditionExpression)
    {
        try
        {
            if (conditionExpression != null)
            {
                var expression = string.Join(" ", conditionExpression.Text);
                var globals = new ScriptGlobals();
                var scriptHandler = new ScriptHandler();
                return await scriptHandler.EvaluateConditionAsync(expression, globals);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error evaluating condition: {ex.Message}");
            return false;
        }

        return false;
    }

    private BpmnStartEvent? FindStartNode()
    {
        return FindElement<BpmnStartEvent>();
    }

    private BpmnFlowElement? FindCurrentNode(string currentNodeId)
    {
        foreach (var item in BpmnDefinitions.Items)
            if (item is BpmnProcess process)
                foreach (var element in process.Items)
                    if (element.id == currentNodeId)
                        return element;
        return null;
    }

    private T? FindElement<T>() where T : BpmnFlowElement
    {
        foreach (var item in BpmnDefinitions.Items)
            if (item is BpmnProcess process)
                foreach (var element in process.Items)
                    if (element is T typedElement)
                        return typedElement;
        return null;
    }
}

public class BpmnRoute
{
    public string StartNode { get; private set; }
    public string EndNode { get; private set; }
    public List<BpmnRoute> Routes { get; private set; }

    public BpmnRoute(string startNodeId, string? endNodeId)
    {
        StartNode = startNodeId;
        EndNode = endNodeId;
        Routes = new List<BpmnRoute>();
    }

    public BpmnRoute Fork(string nextNodeId)
    {
        return new BpmnRoute(StartNode, nextNodeId);
    }
}

public class ScriptGlobals
{
}