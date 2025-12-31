using Novin.Bpmn.Engine.Domain.Common;

public enum JobStatus { Pending, Leased, Running,Canceled, Succeeded, Failed, TimedOut, DeadLetter }

public sealed class Job : BaseAggregateRoot
{
    public Guid ProcessId { get; private set; }
    public Guid TokenId { get; private set; }
    public Guid? NodeInstanceId { get; private set; }

    public string ElementId { get; private set; } = default!;
    public string TaskName { get; private set; } = default!;
    public string Implementation { get; private set; } = default!;

    public JobStatus Status { get; private set; } = JobStatus.Pending;

    public int Attempts { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? LeasedAtUtc { get; private set; }
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    public DateTime? NextAttemptAtUtc { get; private set; }

    public string? ClientId { get; private set; }      // who leased/executed
    public string? LockId { get; private set; }        // lease token
    public DateTime? LockedUntilUtc { get; private set; }

    public string? ErrorMessage { get; private set; }

    public Dictionary<string,string> Payload { get; private set; } = new(StringComparer.Ordinal);
    public Dictionary<string,string> Result  { get; private set; } = new(StringComparer.Ordinal);

    private Job() { }

    public static Job Create(
        Guid processId, Guid tokenId, Guid? nodeInstanceId,
        string elementId, string taskName, string implementation,
        IReadOnlyDictionary<string,string>? payload = null)
    {
        // guards...
        var j = new Job
        {
            ProcessId = processId,
            TokenId = tokenId,
            NodeInstanceId = nodeInstanceId,
            ElementId = elementId,
            TaskName = taskName,
            Implementation = implementation,
            Status = JobStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };

        if (payload != null)
            foreach (var kv in payload) j.Payload[kv.Key] = kv.Value;

        // domain event: JobCreated
        return j;
    }

    public void Lease(string clientId, string lockId, DateTime lockedUntilUtc)
    {
        if (Status != JobStatus.Pending) throw new InvalidOperationException("Job not pending.");
        ClientId = clientId;
        LockId = lockId;
        LockedUntilUtc = lockedUntilUtc;
        LeasedAtUtc = DateTime.UtcNow;
        Status = JobStatus.Leased;
        // event: JobLeased
    }

    public void Start()
    {
        if (Status != JobStatus.Leased) throw new InvalidOperationException("Job not leased.");
        Status = JobStatus.Running;
        StartedAtUtc = DateTime.UtcNow;
        // event: JobStarted
    }

    public void Succeed(IReadOnlyDictionary<string,string>? result = null)
    {
        if (Status != JobStatus.Running) throw new InvalidOperationException("Job not running.");
        if (result != null) foreach (var kv in result) Result[kv.Key] = kv.Value;
        Status = JobStatus.Succeeded;
        CompletedAtUtc = DateTime.UtcNow;
        // event: JobSucceeded
    }

    public void Fail(string error, DateTime? nextAttemptAtUtc = null, bool deadLetter = false)
    {
        if (Status is JobStatus.Succeeded or JobStatus.DeadLetter) return;

        Attempts++;
        ErrorMessage = error;
        CompletedAtUtc = DateTime.UtcNow;

        if (deadLetter)
        {
            Status = JobStatus.DeadLetter;
            NextAttemptAtUtc = null;
        }
        else
        {
            Status = JobStatus.Failed;
            NextAttemptAtUtc = nextAttemptAtUtc;
        }

        // event: JobFailed/JobDeadLettered
    }
}
