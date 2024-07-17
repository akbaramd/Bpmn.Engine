using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Novin.Bpmn.Test;
using Novin.Bpmn.Test.Core;
using Novin.Bpmn.Test.Executors;
using Novin.Bpmn.Test.Executors.Abstracts;
using Novin.Bpmn.Test.Models;

public class BpmnEngine
{
    private readonly ITaskExecutor _scriptTaskExecutor;
    public readonly IUserTaskExecutor _userTaskExecutor;
    private readonly IStartEventExecutor _startEventExecutor;
    private readonly IEndEventExecutor _endEventExecutor;

    public BpmnInstance Instance { get; }
    public List<BpmnBranch> Branches { get; }

    public List<BpmnBranch> ActiveBranches { get; }

    private readonly BpmnExecutionModel _executionModel;

    public BpmnEngine(string filePath)
    {
        try
        {
            var definitions = DeserializeBpmnFile(filePath);
            Branches = new BpmnBranchFinder().GetAllDistinctBranches(definitions).ToList();
            Instance = new BpmnInstance(definitions);
            ActiveBranches = new List<BpmnBranch>();
            _executionModel = new BpmnExecutionModel();

            _scriptTaskExecutor = new ScriptTaskExecutor();
            _userTaskExecutor = new UserTaskExecutor();
            _startEventExecutor = new StartEventExecutor();
            _endEventExecutor = new EndEventExecutor();

            InitializeExecutionModel();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    private BpmnDefinitions DeserializeBpmnFile(string filePath)
    {
        var xmlContent = System.IO.File.ReadAllText(filePath);
        var serializer = new System.Xml.Serialization.XmlSerializer(typeof(BpmnDefinitions),
            "http://www.omg.org/spec/BPMN/20100524/MODEL");
        using var stringReader = new System.IO.StringReader(xmlContent);
        return (BpmnDefinitions)serializer.Deserialize(stringReader)!;
    }

    private void InitializeExecutionModel()
    {
        var startEvent = FindStartEvent();
        var startNode = ConvertElementToNode(startEvent);
        _executionModel.AddActiveRoute(startNode);
        ActiveBranches.Add(Branches.First(x => x.Items.Last() == startEvent.id));
    }

    public BpmnNode ConvertElementToNode(BpmnFlowElement flowElement)
    {
        return new BpmnNode()
        {
            Element = flowElement,
            Id = flowElement.id,
            Executed = false,
            Outgoing = FindOutgoingSequenceFlows(flowElement),
            Incoming = FindIncomingSequenceFlows(flowElement),
            ForkedBranchCount = flowElement is BpmnParallelGateway || flowElement is BpmnInclusiveGateway
                ? FindOutgoingSequenceFlows(flowElement).Count
                : 0
        };
    }

    public BpmnFlowElement FindElementWithId(string id)
    {
        return FindFirstProcess().Items.First(x => x.id.Equals(id));
    }

    private List<BpmnSequenceFlow> FindOutgoingSequenceFlows(BpmnFlowElement flowElement)
    {
        return FindFirstProcess().Items.OfType<BpmnSequenceFlow>().Where(x => x.sourceRef.Equals(flowElement.id))
            .ToList();
    }

    private List<BpmnSequenceFlow> FindIncomingSequenceFlows(BpmnFlowElement flowElement)
    {
        return FindFirstProcess().Items.OfType<BpmnSequenceFlow>().Where(x => x.targetRef.Equals(flowElement.id))
            .ToList();
    }

    private BpmnStartEvent FindStartEvent()
    {
        return FindFirstProcess().Items.OfType<BpmnStartEvent>().First();
    }

    private BpmnProcess FindFirstProcess()
    {
        return Instance.Definitions.Items.OfType<BpmnProcess>().First();
    }

    public async Task<BpmnInstance> ExecuteProcessAsync()
    {
        while (_executionModel.HasActiveRoutes())
        {
            var tasks = _executionModel.GetActiveRoutes().Select(ExecuteAsync).ToList();
            await Task.WhenAll(tasks);

            if (_executionModel.HasPendingUserTasks())
            {
                // Stop execution until user tasks are handled
                break;
            }
        }

        return Instance;
    }

    private async Task ExecuteAsync(BpmnNode currentNode)
    {
        if (currentNode.Executed)
        {
            return;
        }

        if (currentNode.Element != null)
        {
            switch (currentNode.Element)
            {
                case BpmnStartEvent startEvent:
                    await _startEventExecutor.ExecuteAsync(startEvent, this);
                    break;
                case BpmnScriptTask scriptTask:
                    await _scriptTaskExecutor.ExecuteAsync(scriptTask, this);
                    break;
                case BpmnUserTask userTask:
                    await _userTaskExecutor.ExecuteAsync(userTask, this);
                    break;
                case BpmnEndEvent endEvent:
                    await _endEventExecutor.ExecuteAsync(endEvent, this);
                    break;
                case BpmnParallelGateway parallelGateway:
                case BpmnInclusiveGateway inclusiveGateway:
                    // Check if all incoming branches are executed
                    if (!await AreAllIncomingBranchesExecuted(currentNode))
                    {
                        return; // Wait until all incoming branches are executed
                    }

                    break;
            }

            currentNode.Executed = true;
            _executionModel.AddToHistory(currentNode);

            // Update the history of the active branch
            var activeBranch = ActiveBranches.FirstOrDefault(branch => branch.Items.Contains(currentNode.Id));
            if (activeBranch != null)
            {
                activeBranch.History.Push(currentNode.Id);
            }
        }

        _executionModel.RemoveActiveRoute(currentNode);
        var nextRoutes = await FindNextRoutes(currentNode);
        _executionModel.AddActiveRoutes(nextRoutes);
    }

    private async Task<List<BpmnNode>> FindNextRoutes(BpmnNode currentNode)
    {
        var nextRoutes = new List<BpmnNode>();

        if (currentNode.Element is BpmnGateway gateway)
        {
            ActiveBranches.Remove(Branches.First(x => x.Items.First() == gateway.id));

            var gatewayNextRoutes = await FindNextRoute(currentNode);
            foreach (var nextNode in gatewayNextRoutes)
            {
                ActiveBranches.Add(Branches.First(x => x.Items.Last() == nextNode.Id));
            }

            nextRoutes.AddRange(gatewayNextRoutes);
        }
        else
        {
            foreach (var outgoing in currentNode.Outgoing)
            {
                var nextElement = FindElementWithId(outgoing.targetRef);
                var nextNode = ConvertElementToNode(nextElement);
                nextRoutes.Add(nextNode);
            }
        }

        return nextRoutes;
    }

    private async Task<List<BpmnNode>> FindNextRoute(BpmnNode node)
    {
        var nextRoutes = new List<BpmnNode>();

        switch (node.Element)
        {
            case BpmnExclusiveGateway exclusiveGateway:
                nextRoutes = await EvaluateExclusiveGateway(node);
                break;
            case BpmnInclusiveGateway inclusiveGateway:
                nextRoutes = await EvaluateInclusiveGateway(node);
                break;
            case BpmnParallelGateway parallelGateway:
                nextRoutes = await EvaluateParallelGateway(node);
                break;
        }

        return nextRoutes;
    }

    private async Task<List<BpmnNode>> EvaluateExclusiveGateway(BpmnNode node)
    {
        var nextRoutes = new List<BpmnNode>();

        foreach (var nextRoute in node.Outgoing)
        {
            if (!await EvaluateCondition(nextRoute.conditionExpression)) continue;

            nextRoutes.Add(ConvertElementToNode(FindElementWithId(nextRoute.targetRef)));
            break;
        }

        return nextRoutes;
    }

    private async Task<List<BpmnNode>> EvaluateInclusiveGateway(BpmnNode node)
    {
        var nextRoutes = new List<BpmnNode>();

        foreach (var nextRoute in node.Outgoing)
        {
            if (!await EvaluateCondition(nextRoute.conditionExpression)) continue;

            var nextNode = ConvertElementToNode(FindElementWithId(nextRoute.targetRef));
            nextRoutes.Add(nextNode);
        }

        return nextRoutes;
    }

    private async Task<List<BpmnNode>> EvaluateParallelGateway(BpmnNode node)
    {
        var nextRoutes = new List<BpmnNode>();

        foreach (var nextRoute in node.Outgoing)
        {
            var nextNode = ConvertElementToNode(FindElementWithId(nextRoute.targetRef));
            nextRoutes.Add(nextNode);
        }

        return nextRoutes;
    }

    private async Task<bool> AreAllIncomingBranchesExecuted(BpmnNode node)
    {
        // Check if all active branches for the given gateway node have been executed
        var possibleFlows  = node.Incoming.Select(flow=>flow.sourceRef).Count(x => ActiveBranches.SelectMany(c=>c.Items).Contains(x));
        var executedFlows  = node.Incoming.Select(flow=>flow.sourceRef).Count(x => ActiveBranches.SelectMany(c=>c.History).Contains(x));

        return (possibleFlows == executedFlows && possibleFlows > 0);
    }

    private async Task<bool> EvaluateInclusiveGatewayCondition(BpmnNode node)
    {
        foreach (var outgoing in node.Outgoing)
        {
            if (await EvaluateCondition(outgoing.conditionExpression))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<bool> EvaluateCondition(BpmnExpression? conditionExpression)
    {
        try
        {
            if (conditionExpression != null)
            {
                var expression = string.Join(" ", conditionExpression.Text);
                var globals = new ScriptGlobals { Instance = Instance };
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
}

public class ScriptGlobals
{
    public BpmnInstance Instance { get; set; }
}
