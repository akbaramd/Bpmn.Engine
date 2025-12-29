using Novin.Bpmn.Engine.Domain.Common;
using Novin.Bpmn.Engine.Domain.ValueObjects;

public enum IncidentScope { Node, Token, Process }
public enum IncidentCause { TechnicalFailure, BpmnError, Timeout, ExternalSystem, Concurrency }

public sealed class Incident : BaseAggregateRoot
{
    public Guid ProcessId { get; private set; }
    public Guid? TokenId { get; private set; }
    public Guid? NodeInstanceId { get; private set; }
    public Guid? WorkerId { get; private set; }

    public string ElementId { get; private set; } = default!; // BPMN element
    public IncidentScope Scope { get; private set; }
    public IncidentCause Cause { get; private set; }

    public string? ErrorCode { get; private set; } // BPMN errorCode
    public string Message { get; private set; } = default!;
    public IncidentStatus Status { get; private set; }

    public int RetryCount { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime LastOccurredAtUtc { get; private set; }
    public DateTime? ResolvedAtUtc { get; private set; }

    private readonly List<IncidentOccurrence> _occurrences = new();
    public IReadOnlyList<IncidentOccurrence> Occurrences => _occurrences;

    private Incident() { } // EF

    public static Incident Open(
        Guid processId,
        Guid? tokenId,
        Guid? nodeInstanceId,
        Guid? workerId,
        string elementId,
        IncidentScope scope,
        IncidentCause cause,
        string message,
        string? errorCode = null,
        string? stackTrace = null)
    {
        if (processId == Guid.Empty) throw new ArgumentException(nameof(processId));
        if (string.IsNullOrWhiteSpace(elementId)) throw new ArgumentException(nameof(elementId));
        if (string.IsNullOrWhiteSpace(message)) throw new ArgumentException(nameof(message));

        var now = DateTime.UtcNow;

        var inc = new Incident
        {
            Id = Guid.NewGuid(),
            ProcessId = processId,
            TokenId = tokenId,
            NodeInstanceId = nodeInstanceId,
            WorkerId = workerId,
            ElementId = elementId.Trim(),
            Scope = scope,
            Cause = cause,
            Message = message,
            ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? null : errorCode.Trim(),
            Status = IncidentStatus.Open,
            RetryCount = 0,
            CreatedAtUtc = now,
            LastOccurredAtUtc = now
        };

        inc._occurrences.Add(IncidentOccurrence.Create(now, message, stackTrace));

        // inc.AddDomainEvent(new IncidentOpenedEvent(...));
        return inc;
    }

    public void RecordOccurrence(string message, string? stackTrace = null)
    {
        if (Status != IncidentStatus.Open) return;
        var now = DateTime.UtcNow;
        LastOccurredAtUtc = now;
        _occurrences.Add(IncidentOccurrence.Create(now, message, stackTrace));
        // AddDomainEvent(new IncidentOccurredEvent(...));
    }

    public void Retry()
    {
        if (Status != IncidentStatus.Open)
            throw new InvalidOperationException($"Cannot retry in {Status}.");

        RetryCount++;
        LastOccurredAtUtc = DateTime.UtcNow;
        // AddDomainEvent(new IncidentRetriedEvent(...));
    }

    public void Resolve(string? note = null)
    {
        if (Status != IncidentStatus.Open)
            throw new InvalidOperationException($"Cannot resolve in {Status}.");

        Status = IncidentStatus.Resolved;
        ResolvedAtUtc = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(note))
            _occurrences.Add(IncidentOccurrence.Create(ResolvedAtUtc.Value, $"Resolved: {note}", null));
        // AddDomainEvent(new IncidentResolvedEvent(...));
    }

    public void Reopen(string reason)
    {
        if (Status != IncidentStatus.Resolved)
            throw new InvalidOperationException($"Cannot reopen in {Status}.");

        Status = IncidentStatus.Open;
        ResolvedAtUtc = null;
        RecordOccurrence($"Reopened: {reason}");
        // AddDomainEvent(new IncidentReopenedEvent(...));
    }
}

public sealed class IncidentOccurrence
{
    public Guid Id { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public string Message { get; private set; } = default!;
    public string? StackTrace { get; private set; }

    private IncidentOccurrence() { }

    public static IncidentOccurrence Create(DateTime atUtc, string message, string? stackTrace)
        => new IncidentOccurrence
        {
            Id = Guid.NewGuid(),
            OccurredAtUtc = atUtc,
            Message = message,
            StackTrace = stackTrace
        };
}
