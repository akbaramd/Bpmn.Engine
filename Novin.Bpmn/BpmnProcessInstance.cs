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
    public BpmnProcessInstance(string definition, string processElementId)
    {
        Id = Guid.NewGuid();
        ProcessElementId = processElementId;
        DefinitionXml = definition;
    }

    public Guid Id { get;  set; }
    public string ProcessElementId { get;  set; }

    [JsonIgnore]
    public BpmnDefinitions Definition => BpmnDefinitionSerializer.Deserialize(DefinitionXml);

    public string DefinitionXml { get;  set; }
    public string DeploymentKey { get;  set; }
    public dynamic Variables { get;  set; } = new ExpandoObject();
    public Queue<BpmnProcessNode> NextQueue { get;  set; } = new();
    public List<BpmnProcessNode> PendingQueue { get;  set; } = new();
    public Stack<BpmnProcessNode> NodeStack { get;  set; } = new();
    public Stack<BpmnNodeTransition> TransitionStack { get;  set; } = new();
    public Stack<string> Exceptions { get;  set; } = new();
    public bool IsPaused { get;  set; }
    public bool IsStopped { get;  set; }
    public bool IsFinished { get;  set; }

    public static BpmnProcessInstance RestoreState(string savedState)
    {
        return JsonConvert.DeserializeObject<BpmnProcessInstance>(savedState);
    }

    public string SaveState()
    {
        return JsonConvert.SerializeObject(this);
    }

    public List<BpmnNodeState> GetExecutedPathsWithFlows()
    {
        var executedPaths = new Dictionary<string, BpmnNodeState>();

        foreach (var node in NodeStack)
        {
            executedPaths[node.ElementId] = new BpmnNodeState
            {
                ElementId = node.ElementId,
                IsActive = node.IsExecutable &&
                           (node.IncomingFlows.Count == 0 || node.IncomingFlows.Count == node.Instances.Count),
                IsPending = PendingQueue.Any(x=>x.Id.Equals(node.Id)),
                Count = node.Instances.Count
            };

            var transitions = TransitionStack.Where(x => x.TargetNodeId.Equals(node.Id));

            foreach (var transition in transitions)
                if (executedPaths.TryGetValue(transition.ElementId, out var existingTransitionState))
                    existingTransitionState.Count++;
                else
                    executedPaths[transition.ElementId] = new BpmnNodeState
                    {
                        ElementId = transition.ElementId,
                        IsActive = node.IsExecutable ,
                        IsPending = false,
                        Count = 1
                    };
        }

        return executedPaths.Values.ToList();
    }

    public void Pause()
    {
        IsPaused = true;
    }

    public void Resume()
    {
        IsPaused = false;
    }

    public void Stop()
    {
        IsStopped = true;
    }

    public void Finish()
    {
        IsFinished = true;
        IsStopped = true;
    }

    public void SetDeploymentKey(string deploymentKey)
    {
        DeploymentKey = deploymentKey;
    }

    public void SetDefinitionXml(string definitionXml)
    {
        DefinitionXml = definitionXml;
    }
}
