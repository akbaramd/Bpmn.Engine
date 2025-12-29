using System;
using System.Collections.Generic;
using System.Linq;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Domain.DomainServices;

/// <summary>
/// Resolves StartEvent(s) for instantiation based on parsed BPMN definitions.
/// Assumptions (per your model):
/// - Deployment.GetDefinitions() => BpmnDefinitions
/// - BpmnDefinitions.Items contains BpmnRootElement items (Process is one of them)
/// - BpmnProcess.Items contains BPMN elements (including startEvent)
/// - startEvent type is BpmnStartEvent
/// - startEvent has `id` (string)
/// - eventDefinitions are represented by property: "eventDefinition" OR "EventDefinitions" OR "eventDefinitions"
/// </summary>
public interface IBpmnStartResolver
{
    IReadOnlyList<string> GetNoneStartEventIds(Deployment deployment, string processBpmnId);
    bool IsValidStartEvent(Deployment deployment, string processBpmnId, string startElementId);
}

public sealed class BpmnStartResolver : IBpmnStartResolver
{
    public IReadOnlyList<string> GetNoneStartEventIds(Deployment deployment, string processBpmnId)
    {
        if (deployment is null) throw new ArgumentNullException(nameof(deployment));
        if (string.IsNullOrWhiteSpace(processBpmnId)) throw new ArgumentException("processBpmnId required", nameof(processBpmnId));

        var process = GetProcessOrNull(deployment, processBpmnId.Trim());
        if (process is null) return Array.Empty<string>();

        var startEvents = (process.Items ?? Array.Empty<object>())
            .OfType<BpmnStartEvent>()
            .ToList();

        if (startEvents.Count == 0) return Array.Empty<string>();

        // NONE start event => no event definitions
        var ids = startEvents
            .Where(IsNoneStartEvent)
            .Select(se => se.id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return ids;
    }

    public bool IsValidStartEvent(Deployment deployment, string processBpmnId, string startElementId)
    {
        if (deployment is null) throw new ArgumentNullException(nameof(deployment));
        if (string.IsNullOrWhiteSpace(processBpmnId)) return false;
        if (string.IsNullOrWhiteSpace(startElementId)) return false;

        var process = GetProcessOrNull(deployment, processBpmnId.Trim());
        if (process is null) return false;

        var id = startElementId.Trim();

        return (process.Items ?? Array.Empty<object>())
            .OfType<BpmnStartEvent>()
            .Any(se => string.Equals(se.id, id, StringComparison.Ordinal));
    }

    private static BpmnProcess? GetProcessOrNull(Deployment deployment, string processBpmnId)
    {
        var defs = deployment.GetDefinitions();
        if (defs?.Items is null) return null;

        // Your note: Items are BpmnRootElement; must cast to BpmnProcess
        return defs.Items
            .OfType<BpmnProcess>()
            .SingleOrDefault(p => string.Equals(p.id, processBpmnId, StringComparison.Ordinal));
    }

    private static bool IsNoneStartEvent(BpmnStartEvent se)
    {
        // Generated types differ; detect "event definitions" by reflection
        var t = se.GetType();

        var prop =
            t.GetProperty("eventDefinition") ??
            t.GetProperty("EventDefinitions") ??
            t.GetProperty("eventDefinitions") ??
            t.GetProperty("EventDefinition") ??
            t.GetProperty("EventDefinitionRef");

        if (prop is null)
        {
            // If model doesn't expose it, safest default for your engine "normal start"
            // is to treat it as NONE start.
            return true;
        }

        var v = prop.GetValue(se);
        if (v is null) return true;

        if (v is Array a) return a.Length == 0;
        if (v is System.Collections.ICollection c) return c.Count == 0;

        // Unknown shape: assume it HAS definitions (so not none-start)
        return false;
    }
}
