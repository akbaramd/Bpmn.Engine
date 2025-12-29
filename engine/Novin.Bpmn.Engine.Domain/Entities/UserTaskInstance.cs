using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Domain.Entities;

// =======================
// User Task (Human Task)
// =======================

public enum UserTaskStatus
{
    Ready,       // created & visible in inbox
    Claimed,     // claimed by a user (optional)
    InProgress,  // started by assignee/claimer
    Completed,   // submitted
    Canceled     // canceled by engine (token moved, boundary interrupt, terminate, etc.)
}

public enum UserTaskClaimMode
{
    Claim,       // must claim before start (optional)
    DirectAssign // direct assign -> assignee can start/complete
}

public static class UserTaskMeta
{
    // UI/Form
    public const string FormKey       = "formKey";
    public const string FormVersion   = "formVersion";
    public const string UiSchemaRef   = "uiSchemaRef";
    public const string DataSchemaRef = "dataSchemaRef";

    // Assignment/Candidates
    public const string Assignee        = "assignee";
    public const string CandidateUsers  = "candidateUsers";   // CSV
    public const string CandidateGroups = "candidateGroups";  // CSV

    // Business hints
    public const string Description  = "description";
    public const string Priority     = "priority";
    public const string DueDateUtc   = "dueDateUtc";
    public const string ClaimMode    = "claimMode";           // Claim/DirectAssign
    public const string Visibility   = "visibilityPolicy";    // optional
}

public sealed class UserTaskInstance : BaseAggregateRoot
{
    // -------------------- Correlation --------------------
    public Guid ProcessId { get; private set; }
    public Guid TokenId { get; private set; }
    public Guid? NodeInstanceId { get; private set; }

    public string ElementId { get; private set; } = string.Empty; // BPMN element id
    public string TaskName  { get; private set; } = string.Empty;

    // -------------------- State --------------------
    public UserTaskStatus Status { get; private set; } = UserTaskStatus.Ready;

    public DateTime CreatedAtUtc   { get; private set; }
    public DateTime? ClaimedAtUtc  { get; private set; }
    public DateTime? StartedAtUtc  { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public DateTime? CanceledAtUtc { get; private set; }

    public string? ClaimedByUserId  { get; private set; }
    public string? CompletedByUserId { get; private set; }

    public string? CancelReason { get; private set; }

    // -------------------- Metadata / Variables --------------------
    // Metadata is contract for UI/inbox (routing/assignment/form/priority/due/...)
    public Dictionary<string, string> Metadata { get; private set; } = new(StringComparer.Ordinal);

    // Input snapshot and output/result
    public Dictionary<string, string> Variables { get; private set; } = new(StringComparer.Ordinal);

    private UserTaskInstance() { } // EF

    // --------------------------------------------------------------------
    // Factory
    // --------------------------------------------------------------------
    public static UserTaskInstance Create(
        Guid processId,
        Guid tokenId,
        Guid? nodeInstanceId,
        string elementId,
        string taskName,
        UserTaskSpec spec,
        IReadOnlyDictionary<string, string>? payloadVariables = null)
    {
        EnsureRequired(processId, tokenId, elementId, taskName);

        var t = new UserTaskInstance
        {
            ProcessId = processId,
            TokenId = tokenId,
            NodeInstanceId = nodeInstanceId,
            ElementId = elementId,
            TaskName = taskName,
            Status = UserTaskStatus.Ready,
            CreatedAtUtc = DateTime.UtcNow
        };

        // ---- UI/Form ----
        t.SetMeta(UserTaskMeta.FormKey, spec.FormKey, required: true);
        t.SetMeta(UserTaskMeta.FormVersion, spec.FormVersion);
        t.SetMeta(UserTaskMeta.UiSchemaRef, spec.UiSchemaRef);
        t.SetMeta(UserTaskMeta.DataSchemaRef, spec.DataSchemaRef);

        // ---- Assignment / Candidates ----
        t.SetMeta(UserTaskMeta.ClaimMode, spec.ClaimMode.ToString());
        t.SetMeta(UserTaskMeta.Assignee, spec.Assignee);
        t.SetMeta(UserTaskMeta.CandidateUsers, JoinCsv(spec.CandidateUsers));
        t.SetMeta(UserTaskMeta.CandidateGroups, JoinCsv(spec.CandidateGroups));

        // ---- Hints ----
        t.SetMeta(UserTaskMeta.Description, spec.Description);
        t.SetMeta(UserTaskMeta.Priority, spec.Priority?.ToString(System.Globalization.CultureInfo.InvariantCulture));
        t.SetMeta(UserTaskMeta.DueDateUtc, spec.DueDateUtc?.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        t.SetMeta(UserTaskMeta.Visibility, spec.VisibilityPolicy);

        foreach (var kv in spec.CustomMetadata)
            t.SetMeta(kv.Key, kv.Value);

        if (payloadVariables != null)
            t.UpsertVariables(payloadVariables);

        t.AddDomainEvent(new UserTaskCreatedDomainEvent(
            t.Id, t.ProcessId, t.TokenId, t.NodeInstanceId, t.ElementId, t.TaskName,
            new Dictionary<string, string>(t.Metadata, StringComparer.Ordinal),
            DateTime.UtcNow));

        return t;
    }

    // --------------------------------------------------------------------
    // Commands
    // --------------------------------------------------------------------

    public void Claim(string userId)
    {
        EnsureNotTerminal();
        EnsureStatus(UserTaskStatus.Ready);

        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("userId cannot be empty.", nameof(userId));

        EnsureUserCanSee(userId); // candidates/assignee policy

        var claimMode = GetClaimMode();
        if (claimMode == UserTaskClaimMode.DirectAssign)
            throw new InvalidOperationException("DirectAssign tasks do not support claim.");

        ClaimedByUserId = userId;
        ClaimedAtUtc = DateTime.UtcNow;
        Status = UserTaskStatus.Claimed;

        AddDomainEvent(new UserTaskClaimedDomainEvent(
            Id, ProcessId, TokenId, NodeInstanceId, ElementId, userId, ClaimedAtUtc.Value));
    }

    public void Start(string userId)
    {
        EnsureNotTerminal();

        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("userId cannot be empty.", nameof(userId));

        var claimMode = GetClaimMode();

        if (Status == UserTaskStatus.Ready)
        {
            EnsureUserCanSee(userId);

            // Claim-mode requires claim first (unless we allow implicit claim on start)
            if (claimMode == UserTaskClaimMode.Claim)
                throw new InvalidOperationException("Task must be claimed before starting.");

            // DirectAssign: must be assignee (if set)
            EnsureAssigneeIfConfigured(userId);

            // Start implicitly sets claimed-by as actor (optional)
            ClaimedByUserId ??= userId;
            ClaimedAtUtc ??= DateTime.UtcNow;
        }
        else if (Status == UserTaskStatus.Claimed)
        {
            if (!string.Equals(ClaimedByUserId, userId, StringComparison.Ordinal))
                throw new InvalidOperationException("Only claimer can start this task.");
        }
        else
        {
            throw new InvalidOperationException($"Cannot start task in {Status}.");
        }

        Status = UserTaskStatus.InProgress;
        StartedAtUtc = DateTime.UtcNow;

        AddDomainEvent(new UserTaskStartedDomainEvent(
            Id, ProcessId, TokenId, NodeInstanceId, ElementId, userId, StartedAtUtc.Value));
    }

    public void Complete(string userId, IReadOnlyDictionary<string, string>? result = null)
    {
        EnsureNotTerminal();
        EnsureStatus(UserTaskStatus.InProgress);

        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("userId cannot be empty.", nameof(userId));

        // For claimed tasks, enforce claimer
        if (!string.IsNullOrWhiteSpace(ClaimedByUserId) &&
            !string.Equals(ClaimedByUserId, userId, StringComparison.Ordinal))
            throw new InvalidOperationException("Only the task actor can complete this task.");

        // DirectAssign: assignee must match (if configured)
        EnsureAssigneeIfConfigured(userId);

        if (result != null)
            UpsertVariables(result);

        CompletedByUserId = userId;
        CompletedAtUtc = DateTime.UtcNow;
        Status = UserTaskStatus.Completed;

        AddDomainEvent(new UserTaskCompletedDomainEvent(
            Id, ProcessId, TokenId, NodeInstanceId, ElementId, userId, CompletedAtUtc.Value,
            result?.ToDictionary(k => k.Key, v => v.Value, StringComparer.Ordinal)));
    }

    public void Cancel(string reason)
    {
        if (Status == UserTaskStatus.Completed || Status == UserTaskStatus.Canceled)
            return;

        Status = UserTaskStatus.Canceled;
        CanceledAtUtc = DateTime.UtcNow;
        CancelReason = string.IsNullOrWhiteSpace(reason) ? null : reason;

        AddDomainEvent(new UserTaskCanceledDomainEvent(
            Id, ProcessId, TokenId, NodeInstanceId, ElementId, CanceledAtUtc.Value, CancelReason));
    }

    // --------------------------------------------------------------------
    // Metadata / Variables
    // --------------------------------------------------------------------

    public string? GetMeta(string key)
        => Metadata.TryGetValue(key, out var v) ? v : null;

    public void SetMeta(string key, object? value, bool required = false)
    {
        EnsureNotTerminal();

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Metadata key cannot be empty.", nameof(key));

        var s = ConvertToString(value);
        if (required && string.IsNullOrWhiteSpace(s))
            throw new ArgumentException($"Metadata '{key}' is required.", nameof(value));

        if (string.IsNullOrWhiteSpace(s))
        {
            Metadata.Remove(key);
            return;
        }

        Metadata[key] = s;

        AddDomainEvent(new UserTaskMetadataChangedDomainEvent(
            Id, ProcessId, TokenId, NodeInstanceId, ElementId, key, s, DateTime.UtcNow));
    }

    public void UpsertVariables(IReadOnlyDictionary<string, string> values)
    {
        EnsureNotTerminal();
        if (values == null || values.Count == 0) return;

        foreach (var kv in values)
            Variables[kv.Key] = kv.Value ?? string.Empty;

        AddDomainEvent(new UserTaskVariablesUpsertedDomainEvent(
            Id, ProcessId, TokenId, NodeInstanceId, ElementId,
            values.ToDictionary(k => k.Key, v => v.Value ?? string.Empty, StringComparer.Ordinal),
            DateTime.UtcNow));
    }

    public void SetVariable(string name, object? value)
    {
        EnsureNotTerminal();

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Variable name cannot be empty.", nameof(name));

        Variables[name] = ConvertToString(value);

        AddDomainEvent(new UserTaskVariablesUpsertedDomainEvent(
            Id, ProcessId, TokenId, NodeInstanceId, ElementId,
            new Dictionary<string, string>(StringComparer.Ordinal) { [name] = Variables[name] },
            DateTime.UtcNow));
    }

    public string? GetVariable(string name)
        => Variables.TryGetValue(name, out var v) ? v : null;

    // --------------------------------------------------------------------
    // Guards / Policies
    // --------------------------------------------------------------------

    private void EnsureNotTerminal()
    {
        if (Status is UserTaskStatus.Completed or UserTaskStatus.Canceled)
            throw new InvalidOperationException($"UserTask is terminal ({Status}).");
    }

    private void EnsureStatus(UserTaskStatus required)
    {
        if (Status != required)
            throw new InvalidOperationException($"UserTask must be in {required} but is {Status}.");
    }

    private UserTaskClaimMode GetClaimMode()
    {
        var s = GetMeta(UserTaskMeta.ClaimMode);
        if (Enum.TryParse<UserTaskClaimMode>(s, ignoreCase: true, out var m))
            return m;
        return UserTaskClaimMode.Claim; // default
    }

    private void EnsureAssigneeIfConfigured(string userId)
    {
        var assignee = GetMeta(UserTaskMeta.Assignee);
        if (!string.IsNullOrWhiteSpace(assignee) &&
            !string.Equals(assignee, userId, StringComparison.Ordinal))
            throw new InvalidOperationException("Task is assigned to another user.");
    }

    private void EnsureUserCanSee(string userId)
    {
        // If assignee set => only assignee can see/start/claim (policy)
        var assignee = GetMeta(UserTaskMeta.Assignee);
        if (!string.IsNullOrWhiteSpace(assignee))
        {
            if (!string.Equals(assignee, userId, StringComparison.Ordinal))
                throw new InvalidOperationException("Task is assigned to another user.");
            return;
        }

        // CandidateUsers
        var cu = SplitCsv(GetMeta(UserTaskMeta.CandidateUsers));
        if (cu?.Count > 0 && cu.Contains(userId, StringComparer.Ordinal))
            return;

        // CandidateGroups - domain does not know group membership; allow if only groups are set (policy outside)
        var cg = SplitCsv(GetMeta(UserTaskMeta.CandidateGroups));
        if (cg?.Count > 0)
            return;

        // If no assignee/candidates => visible to all (or policy outside). Keep permissive.
    }

    private static void EnsureRequired(Guid processId, Guid tokenId, string elementId, string taskName)
    {
        if (processId == Guid.Empty) throw new ArgumentException("ProcessId cannot be empty", nameof(processId));
        if (tokenId == Guid.Empty) throw new ArgumentException("TokenId cannot be empty", nameof(tokenId));
        if (string.IsNullOrWhiteSpace(elementId)) throw new ArgumentException("ElementId cannot be empty", nameof(elementId));
        if (string.IsNullOrWhiteSpace(taskName)) throw new ArgumentException("TaskName cannot be empty", nameof(taskName));
    }

    private static string ConvertToString(object? value)
    {
        if (value == null) return string.Empty;
        if (value is string s) return s;
        return JsonConvert.SerializeObject(value);
    }

    private static string? JoinCsv(IEnumerable<string>? values)
    {
        if (values == null) return null;
        var list = values.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.Ordinal).ToList();
        return list.Count == 0 ? null : string.Join(",", list);
    }

    private static List<string>? SplitCsv(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return null;
        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }
}

// --------------------------------------------------------------------
// Spec (Value Object) - explicit contract for creating user tasks
// --------------------------------------------------------------------

public sealed record UserTaskSpec(
    string FormKey,
    string? FormVersion = null,
    string? UiSchemaRef = null,
    string? DataSchemaRef = null,

    string? Description = null,
    int? Priority = null,
    DateTime? DueDateUtc = null,

    UserTaskClaimMode ClaimMode = UserTaskClaimMode.Claim,

    string? Assignee = null,
    IReadOnlyList<string>? CandidateUsers = null,
    IReadOnlyList<string>? CandidateGroups = null,

    string? VisibilityPolicy = null,
    IReadOnlyDictionary<string, string>? CustomMetadata = null)
{
    public IReadOnlyDictionary<string, string> CustomMetadata { get; init; }
        = CustomMetadata ?? new Dictionary<string, string>(StringComparer.Ordinal);
}

// --------------------------------------------------------------------
// Domain Events
// --------------------------------------------------------------------

public sealed record UserTaskCreatedDomainEvent(
    Guid UserTaskId,
    Guid ProcessId,
    Guid TokenId,
    Guid? NodeInstanceId,
    string ElementId,
    string TaskName,
    IReadOnlyDictionary<string, string> Metadata,
    DateTime OccurredAtUtc) : IDomainEvent;

public sealed record UserTaskClaimedDomainEvent(
    Guid UserTaskId,
    Guid ProcessId,
    Guid TokenId,
    Guid? NodeInstanceId,
    string ElementId,
    string ClaimedByUserId,
    DateTime OccurredAtUtc) : IDomainEvent;

public sealed record UserTaskStartedDomainEvent(
    Guid UserTaskId,
    Guid ProcessId,
    Guid TokenId,
    Guid? NodeInstanceId,
    string ElementId,
    string StartedByUserId,
    DateTime OccurredAtUtc) : IDomainEvent;

public sealed record UserTaskCompletedDomainEvent(
    Guid UserTaskId,
    Guid ProcessId,
    Guid TokenId,
    Guid? NodeInstanceId,
    string ElementId,
    string CompletedByUserId,
    DateTime OccurredAtUtc,
    IReadOnlyDictionary<string, string>? Result) : IDomainEvent;

public sealed record UserTaskCanceledDomainEvent(
    Guid UserTaskId,
    Guid ProcessId,
    Guid TokenId,
    Guid? NodeInstanceId,
    string ElementId,
    DateTime OccurredAtUtc,
    string? Reason) : IDomainEvent;

public sealed record UserTaskMetadataChangedDomainEvent(
    Guid UserTaskId,
    Guid ProcessId,
    Guid TokenId,
    Guid? NodeInstanceId,
    string ElementId,
    string Key,
    string Value,
    DateTime OccurredAtUtc) : IDomainEvent;

public sealed record UserTaskVariablesUpsertedDomainEvent(
    Guid UserTaskId,
    Guid ProcessId,
    Guid TokenId,
    Guid? NodeInstanceId,
    string ElementId,
    IReadOnlyDictionary<string, string> Upserts,
    DateTime OccurredAtUtc) : IDomainEvent;
