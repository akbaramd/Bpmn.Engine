using Novin.Bpmn.Test;
using Novin.Bpmn.Test.Core;
using Novin.Bpmn.Test.Executors.Abstracts;
using Novin.Bpmn.Test.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class BpmnEngine
{
    private readonly ITaskExecutor _scriptTaskExecutor;
    public readonly IUserTaskExecutor _userTaskExecutor;
    private readonly IStartEventExecutor _startEventExecutor;
    private readonly IEndEventExecutor _endEventExecutor;
    private readonly IBpmnFileDeserializer _fileDeserializer;
    private readonly IBpmnBranchFinder _branchFinder;
    private readonly BpmnDefinitionsHandler _definitionsHandler;

    public BpmnInstance Instance { get; }
    public List<BpmnBranch> Branches { get; }

    public BpmnEngine(string filePath,
                      ITaskExecutor scriptTaskExecutor,
                      IUserTaskExecutor userTaskExecutor,
                      IStartEventExecutor startEventExecutor,
                      IEndEventExecutor endEventExecutor,
                      IBpmnFileDeserializer fileDeserializer)
    {
        _scriptTaskExecutor = scriptTaskExecutor;
        _userTaskExecutor = userTaskExecutor;
        _startEventExecutor = startEventExecutor;
        _endEventExecutor = endEventExecutor;
        _fileDeserializer = fileDeserializer;

        var definitions = _fileDeserializer.Deserialize(filePath);
        _definitionsHandler = new BpmnDefinitionsHandler(definitions);
        _branchFinder = new BpmnBranchFinder(definitions);
        Branches = _branchFinder.GetAllDistinctBranches().ToList();
        Instance = new BpmnInstance(definitions);

        InitializeExecutionModel();
    }

    private void InitializeExecutionModel()
    {
        var startEvent = _definitionsHandler.GetFirstStartEvent();
        var initialBranch = Branches.First(x => x.Items.Last() == startEvent.id);
        Instance._activeBranch.Push(initialBranch);
    }

    private BpmnNode ConvertElementToNode(BpmnFlowElement flowElement)
    {
        return new BpmnNode
        {
            Element = flowElement,
            Id = flowElement.id,
            Executed = false,
            Outgoing = _definitionsHandler.GetOutgoingSequenceFlows(flowElement),
            Incoming = _definitionsHandler.GetIncomingSequenceFlows(flowElement),
            ForkedBranchCount = flowElement is BpmnParallelGateway || flowElement is BpmnInclusiveGateway
                ? _definitionsHandler.GetOutgoingSequenceFlows(flowElement).Count
                : 0
        };
    }

    public async Task<BpmnInstance> ExecuteProcessAsync()
    {
        var tasks = new List<Task>();

        while (Instance._activeBranch.Any())
        {
            var activeRoutes = Instance._activeBranch.ToList();
           
            tasks.AddRange(activeRoutes.Select(ExecuteBranchAsync));
            await Task.WhenAll(tasks);

            if (Instance.HasPendingUserTasks())
            {
                // Stop execution until user tasks are handled
                break;
            }
        }

        return Instance;
    }

    private async Task ExecuteBranchAsync(BpmnBranch branch)
    {
        foreach (var nodeId in branch.Items.Reverse())
        {
            var node = ConvertElementToNode(_definitionsHandler.GetElementById(nodeId));
            branch.History.Push(node.Id);
            await ExecuteNodeAsync(branch,node);
        

         
        }
    }

    private async Task ExecuteNodeAsync(BpmnBranch branch , BpmnNode currentNode)
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
                    // Check if all incoming branches are executed
                    if (!await AreAllIncomingBranchesExecuted(currentNode))
                    {
                        return; // Wait until all incoming branches are executed
                    }
                    break;
            }
            currentNode.Executed = true;
            Console.WriteLine($"- Node : {currentNode.Id}");
            
            // if last node
            if (branch.Items.Count == branch.History.Count)
            {
                await GoToNextBranchAsync(branch);
            }
        }
        
    }

    private async Task GoToNextBranchAsync(BpmnBranch currentBranch)
    {
        Instance._activeBranch.Clear();
        var lastNode = ConvertElementToNode(_definitionsHandler.GetElementById(currentBranch.Items.First()));
        if (lastNode.Element is BpmnGateway gateway)
        {
            await FindNextRoutes(lastNode);

            
        }
    }

    private async Task<List<BpmnNode>> FindNextRoutes(BpmnNode currentNode)
    {
        var nextRoutes = new List<BpmnNode>();

        if (currentNode.Element is BpmnGateway)
        {
            var gatewayNextRoutes = await FindNextRoute(currentNode);
            foreach (var nextNode in gatewayNextRoutes)
            {
                try
                {
                    var branch = _branchFinder.FindBranchContainingNode(nextNode.Id);
                    if (branch != null)
                    {
                        branch.Id = Guid.NewGuid().ToString(); // Renew the branch ID
                        Instance._activeBranch.Push(branch);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
            nextRoutes.AddRange(gatewayNextRoutes);
        }
        else
        {
            foreach (var outgoing in currentNode.Outgoing)
            {
                var nextElement = _definitionsHandler.GetElementById(outgoing.targetRef);
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
            case BpmnExclusiveGateway:
                nextRoutes = await EvaluateExclusiveGateway(node);
                break;
            case BpmnInclusiveGateway:
                nextRoutes = await EvaluateInclusiveGateway(node);
                break;
            case BpmnParallelGateway:
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

            nextRoutes.Add(ConvertElementToNode(_definitionsHandler.GetElementById(nextRoute.targetRef)));
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

            var nextNode = ConvertElementToNode(_definitionsHandler.GetElementById(nextRoute.targetRef));
            nextRoutes.Add(nextNode);
        }

        return nextRoutes;
    }

    private async Task<List<BpmnNode>> EvaluateParallelGateway(BpmnNode node)
    {
        var nextRoutes = new List<BpmnNode>();

        foreach (var nextRoute in node.Outgoing)
        {
            var nextNode = ConvertElementToNode(_definitionsHandler.GetElementById(nextRoute.targetRef));
            nextRoutes.Add(nextNode);
        }

        return nextRoutes;
    }

    private async Task<bool> AreAllIncomingBranchesExecuted(BpmnNode node)
    {
        // Check if all active branches for the given gateway node have been executed
        var possibleFlows = node.Incoming.Select(flow => flow.sourceRef).Count(x =>  Instance._activeBranch.SelectMany(c => c.Items).Contains(x));
        var executedFlows = node.Incoming.Select(flow => flow.sourceRef).Count(x => Instance._activeBranch.SelectMany(c => c.History).Contains(x));

        return (possibleFlows == executedFlows && possibleFlows > 0);
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
