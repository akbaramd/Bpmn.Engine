using Novin.Bpmn.EventSourcing.Core.Topology;
using Novin.Bpmn.Models.Models;
using System;
using System.Collections.Generic;
using System.Linq;

public class FlowTopologyBuilder : IFlowTopologyBuilder
{
    /* --------------------------------------------------------------------
     * PUBLIC API
     * ------------------------------------------------------------------*/
    public List<FlowTopology> Build(Guid deploymentId, BpmnDefinitions definitions)
        => definitions.Items
                      .OfType<BpmnProcess>()
                      .Select(p => Build(deploymentId, p))
                      .ToList();

    public FlowTopology Build(Guid deploymentId, BpmnProcess process)
    {
        var nodes         = new Dictionary<string, FlowNode>();
        var outgoing      = new Dictionary<string, List<string>>();
        var incoming      = new Dictionary<string, List<string>>();
        var sequenceFlows = new Dictionary<string, SequenceFlow>();

        foreach (var item in process.Items)
        {
            switch (item)
            {
                /* ---------- SEQUENCE FLOW ---------- */
                case BpmnSequenceFlow seq:
                    sequenceFlows[seq.id] = BuildSequenceFlow(seq);
                    AddFlow(seq.sourceRef, seq.targetRef, outgoing, incoming);
                    break;

                /* ---------- FLOW NODE ---------- */
                case BpmnFlowNode node:
                    nodes[node.id] = BuildFlowNode(node);
                    break;
            }
        }

        foreach (var n in nodes.Values)
        {
            n.IsJoinNode = incoming.TryGetValue(n.ElementId, out var ins)  && ins.Count  > 1;
            n.IsForkNode = outgoing.TryGetValue(n.ElementId, out var outs) && outs.Count > 1;
        }

        return new FlowTopology
        {
            TopologyId    = Guid.NewGuid(),
            DeploymentId  = deploymentId,
            ProcessId     = process.id,
            ProcessName   = process.name,
            Nodes         = nodes,
            Incoming      = incoming,
            Outgoing      = outgoing,
            SequenceFlows = sequenceFlows
        };
    }

    /* --------------------------------------------------------------------
     * PRIVATE HELPERS
     * ------------------------------------------------------------------*/
    private static SequenceFlow BuildSequenceFlow(BpmnSequenceFlow seq)
    {
        var condition = seq.conditionExpression?.Text?.FirstOrDefault();

        return new SequenceFlow
        {
            Id                  = seq.id,
            SourceRef           = seq.sourceRef,
            TargetRef           = seq.targetRef,
            ConditionExpression = condition,
            Metadata            = new Dictionary<string, object?>
            {
                ["Name"]           = seq.name,
                ["Documentation"]  = seq.documentation?.FirstOrDefault(),
                ["Condition"]      = condition,
            }
        };
    }

    private static FlowNode BuildFlowNode(BpmnFlowNode element)
    {
        var type        = BpmnElementTypeHelper.GetBpmnType(element);
        var scriptTask  = element as BpmnScriptTask;
        var serviceTask = element as BpmnServiceTask;
        var userTask    = element as BpmnUserTask;
        var manualTask  = element as BpmnManualTask;

        string? startEventType = type switch
        {
            string t when t.Contains("MessageStartEvent", StringComparison.OrdinalIgnoreCase) => "Message",
            string t when t.Contains("TimerStartEvent",   StringComparison.OrdinalIgnoreCase) => "Timer",
            string t when t.Contains("SignalStartEvent",  StringComparison.OrdinalIgnoreCase) => "Signal",
            string t when t.Contains("ManualStartEvent",  StringComparison.OrdinalIgnoreCase) => "Manual",
            _ => null
        };

        var meta = new Dictionary<string, object?>
        {
            ["Name"]          = element.name,
            ["Documentation"] = element.documentation?.FirstOrDefault(),
            ["ElementType"]   = type
        };

        /* Task-specific metadata */
        if (scriptTask is not null)
        {
            var scriptExpr = scriptTask.ZeebeScript?.Expression ?? scriptTask.Script?.InnerText;
            meta["TaskType"]       = "Script";
            meta["Script"]         = scriptExpr;
            meta["ScriptLanguage"] = scriptTask.ZeebeScript?.ResultVariable ?? scriptTask.ScriptFormat;
            meta["ZeebeExpression"] = scriptTask.ZeebeScript?.Expression;
            meta["ZeebeResultVariable"] = scriptTask.ZeebeScript?.ResultVariable;
        }
        else if (serviceTask is not null)
        {
            meta["TaskType"]      = "Service";
            meta["Implementation"]= serviceTask.implementation;
        }
        else if (userTask is not null)
        {
            meta["TaskType"]        = "User";
        }
        else if (manualTask is not null)
        {
            meta["TaskType"]          = "Manual";
        }
        else
        {
            meta["TaskType"] = "Generic";
        }

        return new FlowNode
        {
            ElementId      = element.id,
            ElementType    = type,
            StartEventType = startEventType,
            Metadata       = meta
        };
    }

    private static void AddFlow(string src, string tgt,
                                IDictionary<string, List<string>> outDict,
                                IDictionary<string, List<string>> inDict)
    {
        if (!outDict.TryGetValue(src, out var outs))
            outDict[src] = outs = new List<string>();
        outs.Add(tgt);

        if (!inDict.TryGetValue(tgt, out var ins))
            inDict[tgt] = ins = new List<string>();
        ins.Add(src);
    }
}
