using Novin.Bpmn.Models.Models;

public static class BpmnElementTypeHelper
{
    /// <summary>
    /// دریافت رشته نوع BPMN کامل با پیشوند bpmn:
    /// </summary>
    public static string GetBpmnType(BpmnFlowElement node)
    {
        if (node == null) throw new ArgumentNullException(nameof(node));

        var typeName = node.GetType().Name; // یا اگر property ای مثل node.Type وجود داره استفاده کن

        // حذف پیشوند احتمالی "Bpmn" و تبدیل به lower case اول
        if (typeName.StartsWith("Bpmn"))
            typeName = typeName.Substring(4);

        // تبدیل حرف اول به کوچک (camelCase)
        typeName = char.ToLower(typeName[0]) + typeName.Substring(1);

        return $"bpmn:{typeName}";
    }

    /// <summary>
    /// آیا نود از نوع Gateway است؟
    /// </summary>
    public static bool IsGateway(BpmnFlowNode node)
    {
        var bpmnType = GetBpmnType(node);
        return bpmnType.Contains("Gateway", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// آیا نود از نوع StartEvent است؟
    /// </summary>
    public static bool IsStartEvent(BpmnFlowNode node)
    {
        var bpmnType = GetBpmnType(node);
        return bpmnType.EndsWith("StartEvent", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// آیا نود از نوع EndEvent است؟
    /// </summary>
    public static bool IsEndEvent(BpmnFlowNode node)
    {
        var bpmnType = GetBpmnType(node);
        return bpmnType.EndsWith("EndEvent", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// تشخیص نوع دقیق StartEvent (Message, Timer, Signal, Manual)
    /// </summary>
    public static string? GetStartEventType(BpmnFlowNode node)
    {
        var bpmnType = GetBpmnType(node);

        if (!IsStartEvent(node))
            return null;

        if (bpmnType.Contains("messageStartEvent", StringComparison.OrdinalIgnoreCase))
            return "Message";
        if (bpmnType.Contains("timerStartEvent", StringComparison.OrdinalIgnoreCase))
            return "Timer";
        if (bpmnType.Contains("signalStartEvent", StringComparison.OrdinalIgnoreCase))
            return "Signal";
        if (bpmnType.Contains("manualStartEvent", StringComparison.OrdinalIgnoreCase))
            return "Manual";

        return "None";
    }
}
