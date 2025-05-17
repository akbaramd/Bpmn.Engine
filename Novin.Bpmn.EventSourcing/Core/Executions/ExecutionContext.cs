namespace Novin.Bpmn.EventSourcing.Core.Executions;

public class ExecutionContext
{
    public Guid ContextId { get; init; } = Guid.NewGuid();
    public Guid InstanceId { get; init; }
    public Guid? ParentContextId { get; init; }

    private string? _currentElementId;
    /// <summary>
    /// شناسه المان جاری اجرا (Current Element)
    /// </summary>
    public string? CurrentElementId
    {
        get => _currentElementId;
        set
        {
            if (!string.IsNullOrEmpty(value) && value != _currentElementId)
            {
                // هنگام تغییر CurrentElementId، PreviousElementId آپدیت می‌شود
                PreviousElementId = _currentElementId;
                _currentElementId = value;

                // مسیر را هم آپدیت کن (اگر مسیر قبلاً المان نداشته یا المان جدید نبود)
                if (value != null && !Path.Contains(value))
                {
                    Path.Add(value);
                }

                Version++;
            }
        }
    }

    /// <summary>
    /// شناسه المان قبلی در مسیر اجرای فرآیند
    /// </summary>
    public string? PreviousElementId { get; private set; }

    public required bool IsExecutable { get; set; } = true;
    /// <summary>
    /// وضعیت فعلی کانتکست
    /// </summary>
    public ExecutionState State { get; set; } = ExecutionState.Active;

    /// <summary>
    /// متغیرهای محلی کانتکست
    /// </summary>
    public Dictionary<string, object?> LocalVariables { get; set; } = new();

    /// <summary>
    /// نسخه تغییرات برای مدیریت Versioning و بازپخش
    /// </summary>
    public int Version { get; private set; } = 0;

    /// <summary>
    /// مسیر پیمایش شده: لیست شناسه المان‌ها از ابتدا تا CurrentElementId
    /// </summary>
    public List<string> Path { get; private set; } = new();

    /// <summary>
    /// حرکت به المان بعدی (بروزرسانی CurrentElementId و مسیر)
    /// </summary>
    /// <param name="nextElementId"></param>
    public void MoveToNext(string nextElementId)
    {
        // if complated trow errorrs
        if (State == ExecutionState.Completed)
        {
            throw new InvalidOperationException("Cannot move to next element when the context is not active.");
        }
        CurrentElementId = nextElementId;
    }

    /// <summary>
    /// بازگشت به المان قبلی در مسیر (Backtrack)
    /// </summary>
    public void Backtrack()
    {
        if (Path.Count > 1)
        {
            // حذف Current Element
            var removed = Path[^1];
            Path.RemoveAt(Path.Count - 1);

            // ست کردن CurrentElementId به المان قبلی در مسیر
            _currentElementId = Path[^1];
            PreviousElementId = Path.Count > 1 ? Path[^2] : null;

            Version++;
        }
    }

    /// <summary>
    /// کلون کانتکست برای Fork یا Branching
    /// ParentContextId در کلون به ContextId کانتکست فعلی ست می‌شود
    /// </summary>
    /// <returns>نسخه کلون شده جدید</returns>
    public ExecutionContext Clone()
    {
        var clone = new ExecutionContext
        {
            ContextId = Guid.NewGuid(),
            InstanceId = this.InstanceId,
            ParentContextId = this.ContextId,
            State = ExecutionState.Active,
            Version = 0,
            IsExecutable = this.IsExecutable,
            LocalVariables = new Dictionary<string, object?>(this.LocalVariables),
            Path = new List<string>()
        };
        clone.CurrentElementId = this.CurrentElementId;
        // PreviousElementId در setter CurrentElementId آپدیت می‌شود
        return clone;
    }

    /// <summary>
    /// تغییر وضعیت کانتکست به حالت جدید و افزایش نسخه
    /// </summary>
    /// <param name="newState"></param>
    public void UpdateState(ExecutionState newState)
    {
        if (State != newState)
        {
            State = newState;
            Version++;
        }
    }
}

public enum ExecutionState
{
    Active,
    Paused,
    Completed,
    Terminated,
    Failed,
    DeActive
}
