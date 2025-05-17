public class ProcessState
{
    public Guid InstanceId { get; init; }
    public string DeploymentKey { get; init; }
    public Guid DeploymentId { get; init; }
    public string ProcessId { get; init; }

    public ProcessStateStatus Status { get; set; } = ProcessStateStatus.Active;

    public Dictionary<string, object?> Variables { get; set; } = new();

    public int Version { get; set; } = 0;

    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

    // می‌توان لیستی از کانتکست‌ها را نیز ذخیره کرد، یا جداگانه مدیریت نمود
    // public List<ExecutionContext> ExecutionContexts { get; set; } = new();

    public void UpdateVariables(Dictionary<string, object?> localVars)
    {
        foreach (var kv in localVars)
        {
            Variables[kv.Key] = kv.Value;
        }
        Version++;
        LastUpdatedAt = DateTime.UtcNow;
    }
}

public enum ProcessStateStatus
{
    Active,
    Suspended,
    Completed,
    Failed,
    Cancelled,
    Terminated
}