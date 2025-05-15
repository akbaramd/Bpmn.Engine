using Novin.Bpmn.EventSourcing.Core.Topology;
using Novin.Bpmn.Models.Models;
using System;
using System.Collections.Generic;

public class FlowTopologyBuilder : IFlowTopologyBuilder
{
    public List<FlowTopology> Build(Guid deploymentId, BpmnDefinitions definitions)
    {
        var topologies = new List<FlowTopology>();

        foreach (var item in definitions.Items)
        {
            if (item is BpmnProcess process)
            {
                var topology = Build(deploymentId, process);
                topologies.Add(topology);
            }
        }

        return topologies;
    }

    public FlowTopology Build(Guid deploymentId, BpmnProcess process)
    {
        var nodes = new Dictionary<string, FlowNode>();
        var outgoing = new Dictionary<string, List<string>>();
        var incoming = new Dictionary<string, List<string>>();

        foreach (var item in process.Items)
        {
            switch (item)
            {
                case BpmnSequenceFlow seq:
                    AddFlow(seq.sourceRef, seq.targetRef, outgoing, incoming);
                    break;

                case BpmnFlowNode element:
                    // فرض می‌کنیم BpmnFlowNode دارای property به نام name است که نوع BPMN را به صورت رشته (مثلاً "bpmn:scriptTask") دارد
                    var elementType = BpmnElementTypeHelper.GetBpmnType(item);

                    nodes[element.id] = new FlowNode
                    {
                        ElementId = element.id,
                        ElementType = elementType,
                        StartEventType = elementType.Contains("messageStartEvent", StringComparison.OrdinalIgnoreCase) ? "Message" :
                                         elementType.Contains("timerStartEvent", StringComparison.OrdinalIgnoreCase) ? "Timer" :
                                         elementType.Contains("signalStartEvent", StringComparison.OrdinalIgnoreCase) ? "Signal" :
                                         elementType.Contains("manualStartEvent", StringComparison.OrdinalIgnoreCase) ? "Manual" : null,
                    };
                    break;
            }
        }

        // تعیین join و fork بر اساس تعداد ورودی‌ها و خروجی‌ها
        foreach (var node in nodes.Values)
        {
            node.IsJoinNode = incoming.TryGetValue(node.ElementId, out var ins) && ins.Count > 1;
            node.IsForkNode = outgoing.TryGetValue(node.ElementId, out var outs) && outs.Count > 1;
        }

        return new FlowTopology
        {
            TopologyId = Guid.NewGuid(),
            DeploymentId = deploymentId,
            ProcessId = process.id,
            ProcessName = process.name,
            Nodes = nodes,
            Incoming = incoming,
            Outgoing = outgoing
        };
    }

    private void AddFlow(string source, string target,
        Dictionary<string, List<string>> outgoing,
        Dictionary<string, List<string>> incoming)
    {
        if (!outgoing.ContainsKey(source))
            outgoing[source] = new();

        outgoing[source].Add(target);

        if (!incoming.ContainsKey(target))
            incoming[target] = new();

        incoming[target].Add(source);
    }
}
