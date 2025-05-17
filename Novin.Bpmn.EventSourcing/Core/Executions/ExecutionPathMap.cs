using Novin.Bpmn.EventSourcing.Core.Executions;

public class ExecutionTraceMap
{
    public Guid InstanceId { get; init; }
    public List<ExecutionTrace> Traces { get; set; } = new();
}

public class ExecutionTrace
{
    public Guid ExecutionId { get; set; }
    public string? ParentExecutionId { get; set; }
    public List<string> Path { get; set; } = new();           // تمام عناصر پیموده‌شده
    public string? CurrentElementId { get; set; }             // المان جاری
    public ExecutionState State { get; set; }                 // وضعیت نهایی
    public bool IsExecutable { get; set; }
}