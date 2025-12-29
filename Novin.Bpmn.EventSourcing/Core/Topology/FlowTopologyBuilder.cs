using Novin.Bpmn.EventSourcing.Core.Topology;
using Novin.Bpmn.Models.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

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
            // Parse extension elements to find Bonyan extensions
            BonyanIoMapping? bonyanIoMapping = element.extensionElements?.BonyanIoMapping;
            BonyanScript? bonyanScript = scriptTask.BonyanScript;
            
            // If not found in typed properties, try parsing from extensionElements
            if (bonyanIoMapping == null || bonyanScript == null)
            {
                ParseBonyanExtensions(element.extensionElements, out var parsedIoMapping, out var parsedScript);
                bonyanIoMapping ??= parsedIoMapping;
                bonyanScript ??= parsedScript;
            }
            
            // Priority: BonyanScript > ZeebeScript > Standard bpmn:script
            string? scriptExpr = null;
            string? scriptFormat = null;
            
            if (bonyanScript != null)
            {
                // Extract script body - try Body property first, then Text array
                if (bonyanScript.ScriptBody != null)
                {
                    scriptExpr = !string.IsNullOrEmpty(bonyanScript.ScriptBody.Body)
                        ? bonyanScript.ScriptBody.Body
                        : (bonyanScript.ScriptBody.Text != null && bonyanScript.ScriptBody.Text.Length > 0
                            ? string.Join(string.Empty, bonyanScript.ScriptBody.Text)
                            : null);
                }
                
                // Extract script format - try Format property first, then Text array
                if (bonyanScript.ScriptFormat != null)
                {
                    scriptFormat = !string.IsNullOrEmpty(bonyanScript.ScriptFormat.Format)
                        ? bonyanScript.ScriptFormat.Format
                        : (bonyanScript.ScriptFormat.Text != null && bonyanScript.ScriptFormat.Text.Length > 0
                            ? string.Join(string.Empty, bonyanScript.ScriptFormat.Text)
                            : null);
                }
            }
            else if (scriptTask.ZeebeScript != null)
            {
                scriptExpr = scriptTask.ZeebeScript.Expression;
                scriptFormat = scriptTask.ZeebeScript.ResultVariable;
            }
            else
            {
                scriptExpr = scriptTask.Script?.InnerText;
                scriptFormat = scriptTask.ScriptFormat;
            }
            
            meta["TaskType"]       = "Script";
            meta["Script"]         = scriptExpr;
            meta["ScriptLanguage"] = scriptFormat;
            meta["ZeebeExpression"] = scriptTask.ZeebeScript?.Expression;
            meta["ZeebeResultVariable"] = scriptTask.ZeebeScript?.ResultVariable;
            
            // Store Bonyan script details
            if (bonyanScript != null)
            {
                meta["BonyanScriptFormat"] = scriptFormat; // Use the extracted format
                meta["BonyanScriptBody"] = scriptExpr; // Use the extracted script body
            }
            
            // Store Bonyan IO Mapping for variable mapping
            if (bonyanIoMapping != null)
            {
                meta["BonyanIoMapping"] = bonyanIoMapping;
            }
        }
        
        // Check for IO mapping in extension elements for other task types
        if (element.extensionElements?.Any != null)
        {
            ParseBonyanExtensions(element.extensionElements, out var ioMapping, out var script);
            if (ioMapping != null && !meta.ContainsKey("BonyanIoMapping"))
            {
                meta["BonyanIoMapping"] = ioMapping;
            }
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

    /// <summary>
    /// Parses Bonyan extension elements (ioMapping and script) from extensionElements.
    /// </summary>
    private static void ParseBonyanExtensions(BpmnExtensionElements? extensionElements, 
                                             out BonyanIoMapping? ioMapping, 
                                             out BonyanScript? script)
    {
        ioMapping = null;
        script = null;

        if (extensionElements?.Any == null)
            return;

        var serializer = new XmlSerializer(typeof(BonyanIoMapping));
        var scriptSerializer = new XmlSerializer(typeof(BonyanScript));
        const string bonyanNamespace = "http://bonyan.org/schema/bpmn/1.0";

        foreach (var xmlElement in extensionElements.Any)
        {
            if (xmlElement == null)
                continue;

            // Check namespace
            var namespaceUri = xmlElement.NamespaceURI;
            if (namespaceUri != bonyanNamespace)
                continue;

            // Try to deserialize as BonyanIoMapping
            if (xmlElement.LocalName == "ioMapping" && ioMapping == null)
            {
                try
                {
                    using var reader = new XmlNodeReader(xmlElement);
                    ioMapping = serializer.Deserialize(reader) as BonyanIoMapping;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FlowTopologyBuilder] Error deserializing BonyanIoMapping: {ex.Message}");
                }
            }

            // Try to deserialize as BonyanScript
            if (xmlElement.LocalName == "script" && script == null)
            {
                try
                {
                    using var reader = new XmlNodeReader(xmlElement);
                    script = scriptSerializer.Deserialize(reader) as BonyanScript;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FlowTopologyBuilder] Error deserializing BonyanScript: {ex.Message}");
                }
            }
        }
    }
}
