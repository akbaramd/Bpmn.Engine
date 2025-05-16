namespace Novin.Bpmn.EventSourcing.Core.Executions;

public class ExecutionContext
{
    public Guid ContextId { get; init; } = Guid.NewGuid();
    public Guid InstanceId { get; init; }
    public Guid? ParentContextId { get; init; }
    public string CurrentElementId { get; set; }
    public ExecutionState State { get; set; } = ExecutionState.Active;
    public Dictionary<string, object?> LocalVariables { get; set; } = new();
    public int Version { get; set; } = 0;

    // مسیر پیمایش شده: لیست شناسه المان‌ها از ابتدا تا CurrentElementId
    public List<string> Path { get; set; } = new();

    // اضافه کردن المان جدید به مسیر (به روز رسانی CurrentElementId)
    public void MoveToNext(string nextElementId)
    {
        if (!string.IsNullOrEmpty(CurrentElementId) && !Path.Contains(CurrentElementId))
            Path.Add(CurrentElementId);

        CurrentElementId = nextElementId;
        Version++;
    }

    // بازگشت به المان قبلی در مسیر
    public string? GetPreviousElement()
    {
        if (Path.Count == 0) return null;
        return Path[^1]; // آخرین المان مسیر
    }

    // حذف آخرین المان مسیر (در صورت نیاز به backtrack)
    public void RemoveLastFromPath()
    {
        if (Path.Count > 0)
            Path.RemoveAt(Path.Count - 1);
    }
}

public enum ExecutionState
{
    Active,
    Paused,
    Completed,
    Terminated,
    Failed
}