namespace Novin.Bpmn.EventSourcing.Core.Executions;

public class ExecutionContext
{
    public Guid ContextId { get; init; } = Guid.NewGuid();              // شناسه یکتا برای هر شاخه اجرا
    public Guid InstanceId { get; init; }                             // شناسه فرآیند اصلی
    public Guid? ParentContextId { get; init; }                       // اگر در Fork ایجاد شده، ارجاع به والد
    public string CurrentElementId { get; set; }                        // آخرین المان در حال اجرا
    public ExecutionState State { get; set; } = ExecutionState.Active;  // وضعیت اجرا
    public Dictionary<string, object?> LocalVariables { get; set; } = new();  // متغیرهای محلی
    public int Version { get; set; } = 0;                               // برای بازسازی و تغییرات
}

public enum ExecutionState
{
    Active,
    Paused,
    Completed,
    Terminated,
    Failed
}