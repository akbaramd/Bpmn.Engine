using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using Newtonsoft.Json;
using Novin.Bpmn.Core;
using Novin.Bpmn.Models;

namespace Novin.Bpmn;

public class BpmnProcessInstance
{
    // Constructor initializes a new process instance with the given definition and process element ID.
    public BpmnProcessInstance(string definition, string processElementId)
    {
        Console.WriteLine("Initializing BpmnProcessInstance with definition and processElementId");
        Id = Guid.NewGuid(); // Assign a unique identifier to the instance.
        ProcessElementId = processElementId; // The ID of the process element this instance corresponds to.
        DefinitionXml = definition; // The BPMN XML definition for this process instance.
    }

    public Guid Id { get; set; } // Unique ID of the process instance.
    public string ProcessElementId { get; set; } // ID of the process element being executed.

    [JsonIgnore] // Exclude from JSON serialization to avoid redundancy.
    public BpmnDefinitions Definition
    {
        get
        {
            Console.WriteLine("Deserializing definition from XML");
            return BpmnDefinitionSerializer.Deserialize(DefinitionXml);
        }
    }

    public string DefinitionXml { get; set; } // BPMN XML definition.
    public string DeploymentKey { get; set; } // Deployment key for identifying this instance.
    public dynamic Variables { get; set; } = new ExpandoObject(); // Dynamic object to hold process variables.

    // Queues and stacks to manage the state of nodes in the process.
    public Queue<BpmnProcessNode> NextQueue { get; set; } = new(); // Queue for nodes ready for execution.
    public List<BpmnProcessNode> PendingQueue { get; set; } = new(); // List of nodes pending execution.
    public List<BpmnProcessNode> FailedQueue { get; set; } = new(); // List of nodes that encountered errors.
    public Stack<BpmnProcessNode> NodeStack { get; set; } = new(); // Stack to maintain execution history.
    public Stack<BpmnNodeTransition> TransitionStack { get; set; } = new(); // Stack to track transitions.

    // Aggregate exceptions across all nodes in the stack.
    public List<string> NodeExceptions
    {
        get
        {
            Console.WriteLine("Collecting node exceptions");
            return NodeStack.SelectMany(x => x.Exceptions).ToList();
        }
    }

    public bool IsPaused { get; set; } // Indicates if the process instance is paused.
    public bool IsStopped { get; set; } // Indicates if the process instance is stopped.
    public bool IsFinished { get; set; } // Indicates if the process instance has completed execution.

    // Restore a process instance from a saved JSON state.
    public static BpmnProcessInstance RestoreState(string savedState)
    {
        Console.WriteLine("Restoring process instance from saved state");
        return JsonConvert.DeserializeObject<BpmnProcessInstance>(savedState);
    }

    // Save the current state of the process instance to a JSON string.
    public string SaveState()
    {
        Console.WriteLine("Saving process instance state to JSON");
        return JsonConvert.SerializeObject(this);
    }

    // Retrieve paths and flows executed in the process instance.
    public List<BpmnNodeState> GetExecutedPathsWithFlows()
    {
        var executedPaths = new Dictionary<string, BpmnNodeState>();

        foreach (var node in NodeStack)
        {
            // Record the current node state.
            executedPaths[node.ElementId] = new BpmnNodeState
            {
                ElementId = node.ElementId,
                IsActive = node.IsExecutable &&
                           (node.IncomingFlows.Count == 0 || node.IncomingFlows.Count == node.Instances.Count),
                IsPending = PendingQueue.Any(x => x.Id.Equals(node.Id)),
                Count = node.Instances.Count
            };

            // Record transitions leading to this node.
            var transitions = TransitionStack.Where(x => x.TargetNodeId.Equals(node.Id));

            foreach (var transition in transitions)
            {
                if (executedPaths.TryGetValue(transition.ElementId, out var existingTransitionState))
                    existingTransitionState.Count++;
                else
                {
                    var startNode = NodeStack.First(x => x.Id.Equals(transition.SourceNodeId));
                    executedPaths[transition.ElementId] = new BpmnNodeState
                    {
                        ElementId = transition.ElementId,
                        IsActive = node.IsExecutable && startNode.IsExecutable,
                        IsPending = PendingQueue.Any(x => x.Id.Equals(node.Id)),
                        Count = 1
                    };
                }
            }
        }
        
        return executedPaths.Values.ToList();
    }

    // Mark the process instance as paused.
    public void Pause()
    {
        Console.WriteLine("Pausing process instance");
        IsPaused = true;
    }

    // Resume the process instance.
    public void Resume()
    {
        Console.WriteLine("Resuming process instance");
        IsPaused = false;
    }

    // Stop the process instance completely.
    public void Stop()
    {
        Console.WriteLine("Stopping process instance");
        IsStopped = true;
    }

    // Mark the process instance as finished.
    public void Finish()
    {
        Console.WriteLine("Finishing process instance");
        IsFinished = true;
        IsStopped = true;
    }

    // Set the deployment key for the process instance.
    public void SetDeploymentKey(string deploymentKey)
    {
        Console.WriteLine($"Setting deployment key: {deploymentKey}");
        DeploymentKey = deploymentKey;
    }

    // Update the definition XML of the process instance.
    public void SetDefinitionXml(string definitionXml)
    {
        Console.WriteLine("Updating definition XML");
        DefinitionXml = definitionXml;
    }

    // Check if the process instance is fresh (no nodes or transitions).
    public bool IsFreshInstance()
    {
        Console.WriteLine("Checking if instance is fresh");
        return NodeStack.Count == 0 && NextQueue.Count == 0 && TransitionStack.Count == 0;
    }

    // Check if the process instance is currently in progress.
    public bool IsInProgress()
    {
        Console.WriteLine("Checking if instance is in progress");
        return NodeStack.Count > 0 || NextQueue.Count > 0 || PendingQueue.Count > 0;
    }

    // Add a node to the next execution queue.
    public void AddNodeToQueue(BpmnProcessNode node)
    {
        Console.WriteLine($"Adding node {node.ElementId} to next queue");
        NextQueue.Enqueue(node);
    }

    // Add a node to the pending list.
    public void AddNodeToPending(BpmnProcessNode node)
    {
        Console.WriteLine($"Adding node {node.ElementId} to pending queue");
        PendingQueue.Add(node);
    }

    // Add a node to the failed list.
    public void AddNodeToFailed(BpmnProcessNode node)
    {
        Console.WriteLine($"Adding node {node.ElementId} to failed queue");
        FailedQueue.Add(node);
    }

    // Clear all queues for the process instance.
    public void ClearQueues()
    {
        Console.WriteLine("Clearing all queues");
        NextQueue.Clear();
        PendingQueue.Clear();
        FailedQueue.Clear();
    }

    // Reset the process instance to its initial state.
    public void Reset()
    {
        Console.WriteLine("Resetting process instance");
        ClearQueues();
        NodeStack.Clear();
        TransitionStack.Clear();
        Variables = new ExpandoObject();
        IsPaused = false;
        IsStopped = false;
        IsFinished = false;
    }

    // Retrieve the next node from the queue, or null if none exists.
    public BpmnProcessNode GetNextNode()
    {
        Console.WriteLine("Retrieving next node from queue");
        return NextQueue.Count > 0 ? NextQueue.Dequeue() : null;
    }

    // Retry all failed nodes by moving them back to the next execution queue.
    public void RetryFailedNodes()
    {
        Console.WriteLine("Retrying all failed nodes");
        foreach (var node in FailedQueue.ToList())
        {
            Console.WriteLine($"Retrying node {node.ElementId}");
            FailedQueue.Remove(node);
            AddNodeToQueue(node);
        }
    }
}
