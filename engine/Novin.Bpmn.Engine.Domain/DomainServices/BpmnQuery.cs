

using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Models.Models;

/// <summary>
/// Domain-facing read-only query service over parsed BPMN definitions.
/// Pure (stateless) and deterministic: no DB, no IO, no caching assumptions.
/// 
/// Model assumptions (based on your note):
/// - Deployment.GetDefinitions() returns BpmnDefinitions
/// - BpmnDefinitions.Items contains BpmnRootElement (process is a root element)
/// - BpmnProcess.Items contains BPMN node elements (flow nodes, events, gateways, tasks, etc.)
/// - Each node element has an id
/// </summary>
public interface IBpmnQuery
{
    // -------- Root (process) --------
    BpmnProcess GetProcessOrThrow(Deployment deployment, string processBpmnId);

    bool TryGetProcess(Deployment deployment, string processBpmnId, out BpmnProcess? process);

    IReadOnlyList<BpmnProcess> GetAllProcesses(Deployment deployment);

    // -------- Elements (within a process) --------
    TElement GetElementOrThrow<TElement>(Deployment deployment, string processBpmnId, string elementId)
        where TElement : class;

    object GetElementOrThrow(Deployment deployment, string processBpmnId, string elementId);

    bool TryGetElement(Deployment deployment, string processBpmnId, string elementId, out object? element);

    bool TryGetElement<TElement>(Deployment deployment, string processBpmnId, string elementId, out TElement? element)
        where TElement : class;

    IReadOnlyList<object> GetAllElements(Deployment deployment, string processBpmnId);

    IReadOnlyList<TElement> GetAllElementsOfType<TElement>(Deployment deployment, string processBpmnId)
        where TElement : class;

    // -------- Start events --------
    IReadOnlyList<string> GetNoneStartEventIds(Deployment deployment, string processBpmnId);
    bool IsStartEvent(Deployment deployment, string processBpmnId, string startElementId);

    // -------- Sequence Flows (optional but useful for navigation) --------
    IEnumerable<BpmnSequenceFlow> GetOutgoingSequenceFlows(Deployment deployment, string processBpmnId, string fromElementId);
    IEnumerable<BpmnSequenceFlow> GetIncomingSequenceFlows(Deployment deployment, string processBpmnId, string toElementId);
}

public sealed class BpmnQuery : IBpmnQuery
{
    public BpmnProcess GetProcessOrThrow(Deployment deployment, string processBpmnId)
    {
        if (!TryGetProcess(deployment, processBpmnId, out var p) || p is null)
            throw new InvalidOperationException($"BPMN process '{processBpmnId}' not found in deployment '{deployment.Id}'.");
        return p;
    }

    public bool TryGetProcess(Deployment deployment, string processBpmnId, out BpmnProcess? process)
    {
        process = null;
        if (deployment is null) throw new ArgumentNullException(nameof(deployment));
        if (string.IsNullOrWhiteSpace(processBpmnId)) return false;

        var defs = deployment.GetDefinitions();
        if (defs?.Items is null) return false;

        // RootElements live in definitions.Items as BpmnRootElement; processes are among them.
        process = defs.Items
            .OfType<BpmnProcess>()
            .SingleOrDefault(p => string.Equals(p.id, processBpmnId.Trim(), StringComparison.Ordinal));

        return process != null;
    }

    public IReadOnlyList<BpmnProcess> GetAllProcesses(Deployment deployment)
    {
        if (deployment is null) throw new ArgumentNullException(nameof(deployment));

        var defs = deployment.GetDefinitions();
        if (defs?.Items is null) return Array.Empty<BpmnProcess>();

        return defs.Items.OfType<BpmnProcess>().ToList();
    }

    public TElement GetElementOrThrow<TElement>(Deployment deployment, string processBpmnId, string elementId)
        where TElement : class
    {
        if (!TryGetElement<TElement>(deployment, processBpmnId, elementId, out var el) || el is null)
        {
            var expected = typeof(TElement).Name;
            throw new InvalidOperationException(
                $"Element '{elementId}' not found in process '{processBpmnId}' as '{expected}'.");
        }
        return el;
    }

    public object GetElementOrThrow(Deployment deployment, string processBpmnId, string elementId)
    {
        if (!TryGetElement(deployment, processBpmnId, elementId, out var el) || el is null)
            throw new InvalidOperationException($"Element '{elementId}' not found in process '{processBpmnId}'.");
        return el;
    }

    public bool TryGetElement(Deployment deployment, string processBpmnId, string elementId, out object? element)
    {
        element = null;
        if (deployment is null) throw new ArgumentNullException(nameof(deployment));
        if (string.IsNullOrWhiteSpace(processBpmnId)) return false;
        if (string.IsNullOrWhiteSpace(elementId)) return false;

        if (!TryGetProcess(deployment, processBpmnId, out var process) || process is null)
            return false;

        var items = process.Items;
        if (items is null || items.Length == 0) return false;

        var targetId = elementId.Trim();

        // We cannot assume a common interface, so we locate by reflection against "id"/"Id".
        // This keeps IBpmnQuery stable even if the generated BPMN classes vary.
        element = items.Cast<object>()
            .FirstOrDefault(x => HasId(x, targetId));

        return element != null;
    }

    public bool TryGetElement<TElement>(Deployment deployment, string processBpmnId, string elementId, out TElement? element)
        where TElement : class
    {
        element = null;

        if (!TryGetElement(deployment, processBpmnId, elementId, out var obj) || obj is null)
            return false;

        element = obj as TElement;
        return element != null;
    }

    public IReadOnlyList<object> GetAllElements(Deployment deployment, string processBpmnId)
    {
        var p = GetProcessOrThrow(deployment, processBpmnId);
        return (p.Items ?? Array.Empty<object>()).Cast<object>().ToList();
    }

    public IReadOnlyList<TElement> GetAllElementsOfType<TElement>(Deployment deployment, string processBpmnId)
        where TElement : class
    {
        var p = GetProcessOrThrow(deployment, processBpmnId);
        return (p.Items ?? Array.Empty<object>()).OfType<TElement>().ToList();
    }

    // -------- Start events --------

    public IReadOnlyList<string> GetNoneStartEventIds(Deployment deployment, string processBpmnId)
    {
        var p = GetProcessOrThrow(deployment, processBpmnId);

        // In many generated models: StartEvent is BpmnStartEvent (or similar).
        // If your model uses a different type name, just adjust here.
        var starts = (p.Items ?? Array.Empty<object>()).OfType<BpmnStartEvent>().ToList();
        if (starts.Count == 0) return Array.Empty<string>();

        var ids = starts
            .Where(se => IsNoneStartEvent(se))
            .Select(se => se.id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return ids;
    }

    public bool IsStartEvent(Deployment deployment, string processBpmnId, string startElementId)
    {
        if (string.IsNullOrWhiteSpace(startElementId)) return false;

        var p = GetProcessOrThrow(deployment, processBpmnId);
        var id = startElementId.Trim();

        return (p.Items ?? Array.Empty<object>())
            .OfType<BpmnStartEvent>()
            .Any(se => string.Equals(se.id, id, StringComparison.Ordinal));
    }

    private static bool IsNoneStartEvent(BpmnStartEvent se)
    {
        // Generated models vary:
        // - EventDefinitions might be `eventDefinition` array
        // - Or `eventDefinition` list
        // - Or `Items` with eventDefinition types
        //
        // We'll treat "none start" as: no event definitions present.
        // Adjust if your model encodes differently.
        var prop = se.GetType().GetProperty("eventDefinition")
                   ?? se.GetType().GetProperty("EventDefinitions")
                   ?? se.GetType().GetProperty("eventDefinitions");

        if (prop == null) return true; // if not present, assume none-start

        var v = prop.GetValue(se);
        if (v == null) return true;

        if (v is Array a) return a.Length == 0;
        if (v is System.Collections.ICollection c) return c.Count == 0;

        // unknown shape => treat as "has definition"
        return false;
    }

    // -------- SequenceFlows --------

    public IEnumerable<BpmnSequenceFlow> GetOutgoingSequenceFlows(Deployment deployment, string processBpmnId, string fromElementId)
    {
        if (string.IsNullOrWhiteSpace(fromElementId)) return Enumerable.Empty<BpmnSequenceFlow>();
        var p = GetProcessOrThrow(deployment, processBpmnId);
        var from = fromElementId.Trim();

        // If your model uses BpmnSequenceFlow for sequenceFlow nodes inside process.Items
        var flows = (p.Items ?? Array.Empty<object>()).OfType<BpmnSequenceFlow>();
        return flows.Where(f => string.Equals(f.sourceRef, from, StringComparison.Ordinal));
    }

    public IEnumerable<BpmnSequenceFlow> GetIncomingSequenceFlows(Deployment deployment, string processBpmnId, string toElementId)
    {
        if (string.IsNullOrWhiteSpace(toElementId)) return Enumerable.Empty<BpmnSequenceFlow>();
        var p = GetProcessOrThrow(deployment, processBpmnId);
        var to = toElementId.Trim();

        var flows = (p.Items ?? Array.Empty<object>()).OfType<BpmnSequenceFlow>();
        return flows.Where(f => string.Equals(f.targetRef, to, StringComparison.Ordinal));
    }

    // -------- Id matching (robust against generated class differences) --------

    private static bool HasId(object obj, string expectedId)
    {
        var t = obj.GetType();

        // Common in generated BPMN C# classes: `id` (lowercase)
        var p1 = t.GetProperty("id");
        if (p1?.PropertyType == typeof(string))
        {
            var v = (string?)p1.GetValue(obj);
            if (string.Equals(v, expectedId, StringComparison.Ordinal)) return true;
        }

        // Sometimes `Id` (PascalCase)
        var p2 = t.GetProperty("Id");
        if (p2?.PropertyType == typeof(string))
        {
            var v = (string?)p2.GetValue(obj);
            if (string.Equals(v, expectedId, StringComparison.Ordinal)) return true;
        }

        return false;
    }
}
