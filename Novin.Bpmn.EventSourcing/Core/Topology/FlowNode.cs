public class FlowNode
{
    public string ElementId { get; init; } = default!;

    /// <summary>
    /// نوع عنصر به‌صورت رشته، مثلاً "bpmn:scriptTask", "bpmn:messageStartEvent"
    /// </summary>
    public string ElementType { get; init; } = default!;

    /// <summary>
    /// آیا گره یک Start Event است
    /// </summary>
    public bool IsStartEvent => ElementType.EndsWith("StartEvent", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// نوع دقیق Start Event (Message, Timer, Signal, Manual, None)
    /// </summary>
    public string? StartEventType { get; init; }

    /// <summary>
    /// آیا گره یک End Event است
    /// </summary>
    public bool IsEndEvent => ElementType.EndsWith("EndEvent", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// آیا گره Gateway است (Exclusive, Parallel, Inclusive, Complex)
    /// </summary>
    public bool IsGateway => ElementType.Contains("Gateway", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// آیا گره نقطه Join است (مبنا بر تعداد incoming)
    /// </summary>
    public bool IsJoinNode { get; set; }

    /// <summary>
    /// آیا گره نقطه Fork است (مبنا بر تعداد outgoing)
    /// </summary>
    public bool IsForkNode { get; set; }

    /// <summary>
    /// آیا گره multi-instance است (فعال یا غیرفعال)
    /// </summary>

    /// <summary>
    /// متادیتاهای اضافی
    /// </summary>
    public Dictionary<string, object?> Metadata { get; init; } = new();
}