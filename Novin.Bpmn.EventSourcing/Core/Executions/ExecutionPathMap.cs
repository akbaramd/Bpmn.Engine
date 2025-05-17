using Novin.Bpmn.EventSourcing.Core.Executions;

public class ExecutionTraceMap
{
    public Guid InstanceId { get; init; }
    public List<ExecutionTrace> Traces { get; set; } = new();
    public List<SequenceFlowTrace> SequenceFlows { get; set; } = new();
}

public class ExecutionTrace
{
    public Guid ExecutionId { get; set; }
    public string? ParentExecutionId { get; set; }
    public List<string> Path { get; set; } = new();           // تمام عناصر پیموده‌شده
    public string? CurrentElementId { get; set; }             // المان جاری
    public ExecutionState State { get; set; }                 // وضعیت نهایی
    public bool IsExecutable { get; set; }
    public int SequenceId { get; set; }                       // شناسه ترتیب اجرا
    public DateTime LastUpdated { get; set; }                 // آخرین بروزرسانی
}

public class SequenceFlowTrace
{
    public string FlowId { get; set; } = string.Empty;         // شناسه مسیر جریان
    public string SourceId { get; set; } = string.Empty;       // شناسه عنصر مبدا
    public string TargetId { get; set; } = string.Empty;       // شناسه عنصر مقصد
    public bool IsExecutable { get; set; }                     // قابل اجرا بودن مسیر
    public ExecutionState State { get; set; }                  // وضعیت مسیر
    public int SequenceId { get; set; }                        // شناسه ترتیب اجرا
    public Guid? RelatedExecutionId { get; set; }              // شناسه اجرای مرتبط
}