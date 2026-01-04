// Domain/Entities/UserTaskInstance.cs  (final: Variables + Metadata as single JSON blobs)
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Novin.Bpmn.Engine.Domain.Common;
using Novin.Bpmn.Engine.Domain.ValueObjects;

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

    public string ElementId { get; private set; } = string.Empty;
    public string TaskName  { get; private set; } = string.Empty;

    // -------------------- State --------------------
    public UserTaskStatus Status { get; private set; } = UserTaskStatus.Ready;

    public DateTime CreatedAtUtc    { get; private set; }
    public DateTime? ClaimedAtUtc   { get; private set; }
    public DateTime? StartedAtUtc   { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public DateTime? CanceledAtUtc  { get; private set; }

    public string? ClaimedByUserId   { get; private set; }
    public string? CompletedByUserId { get; private set; }

    public string? CancelReason { get; private set; }

    // =========================
    // Variables (SINGLE JSON)
    // =========================
    private string _variablesJson = "{}";
    private JsonObject? _variablesObj;
    private bool _variablesLoaded;

    // EF-friendly property (maps to a single column)
    public string VariablesJson
    {
        get => _variablesJson;
        private set
        {
            _variablesJson = string.IsNullOrWhiteSpace(value) ? "{}" : value;
            _variablesLoaded = false;
            _variablesObj = null;
        }
    }

    public JsonObject VariablesObject => GetVarsClone();

    // =========================
    // Metadata (SINGLE JSON)
    // =========================
    private string _metadataJson = "{}";
    private JsonObject? _metadataObj;
    private bool _metadataLoaded;

    public string MetadataJson
    {
        get => _metadataJson;
        private set
        {
            _metadataJson = string.IsNullOrWhiteSpace(value) ? "{}" : value;
            _metadataLoaded = false;
            _metadataObj = null;
        }
    }

    public JsonObject MetadataObject => GetMetaClone();

    private UserTaskInstance()
    {
        CreatedAtUtc = DateTime.UtcNow;

        _variablesJson = "{}";
        _variablesLoaded = false;
        _variablesObj = null;

        _metadataJson = "{}";
        _metadataLoaded = false;
        _metadataObj = null;
    }

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
        IReadOnlyDictionary<string, object>? payloadVariables = null)
    {
        EnsureRequired(processId, tokenId, elementId, taskName);

        var t = new UserTaskInstance
        {
            ProcessId = processId,
            TokenId = tokenId,
            NodeInstanceId = nodeInstanceId,
            ElementId = elementId.Trim(),
            TaskName = taskName.Trim(),
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
        t.SetMeta(UserTaskMeta.Priority, spec.Priority?.ToString(CultureInfo.InvariantCulture));
        t.SetMeta(UserTaskMeta.DueDateUtc, spec.DueDateUtc?.ToString("O", CultureInfo.InvariantCulture));
        t.SetMeta(UserTaskMeta.Visibility, spec.VisibilityPolicy);

        foreach (var kv in spec.CustomMetadata)
            t.SetMeta(kv.Key, kv.Value);

        if (payloadVariables != null && payloadVariables.Count > 0)
            t.UpsertVariables(payloadVariables.ToDictionary(k => k.Key, v => (object?)v.Value));

        t.AddDomainEvent(new UserTaskCreatedDomainEvent(
            t.Id, t.ProcessId, t.TokenId, t.NodeInstanceId, t.ElementId, t.TaskName,
            t.GetMetadataSnapshot(), // 👈 still event expects string dictionary
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

        EnsureUserCanSee(userId);

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

            if (claimMode == UserTaskClaimMode.Claim)
                throw new InvalidOperationException("Task must be claimed before starting.");

            EnsureAssigneeIfConfigured(userId);

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

    public void Complete(string userId, IReadOnlyDictionary<string, object?>? result = null)
    {
        EnsureNotTerminal();
        EnsureStatus(UserTaskStatus.InProgress);

        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("userId cannot be empty.", nameof(userId));

        if (!string.IsNullOrWhiteSpace(ClaimedByUserId) &&
            !string.Equals(ClaimedByUserId, userId, StringComparison.Ordinal))
            throw new InvalidOperationException("Only the task actor can complete this task.");

        EnsureAssigneeIfConfigured(userId);

        if (result != null && result.Count > 0)
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
    // Metadata API (JSON blob)
    // --------------------------------------------------------------------
    public string? GetMeta(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        var k = NormalizeKey(key);

        var meta = Meta();
        if (!meta.TryGetPropertyValue(k, out var node) || node is null)
            return null;

        // Metadata is *string contract*, store as JSON string value.
        if (node is JsonValue v && v.TryGetValue<string>(out var s))
            return s;

        // fallback: stable JSON string
        return JsonVariableCodec.ToStableJson(node);
    }

    public void SetMeta(string key, object? value, bool required = false)
    {
        EnsureNotTerminal();

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Metadata key cannot be empty.", nameof(key));

        var k = EnsureKey(key);
        var s = ConvertToString(value);

        if (required && string.IsNullOrWhiteSpace(s))
            throw new ArgumentException($"Metadata '{key}' is required.", nameof(value));

        var meta = Meta();

        if (string.IsNullOrWhiteSpace(s))
        {
            if (meta.Remove(k))
            {
                FlushMeta(meta);
            }
            return;
        }

        // Compare old vs new (avoid no-op updates)
        if (meta.TryGetPropertyValue(k, out var oldNode) &&
            oldNode is JsonValue ov && ov.TryGetValue<string>(out var oldStr) &&
            string.Equals(oldStr, s, StringComparison.Ordinal))
        {
            return;
        }

        meta[k] = JsonValue.Create(s);
        FlushMeta(meta);

        AddDomainEvent(new UserTaskMetadataChangedDomainEvent(
            Id, ProcessId, TokenId, NodeInstanceId, ElementId, k, s, DateTime.UtcNow));
    }

    public IReadOnlyDictionary<string, string> GetMetadataSnapshot()
    {
        var meta = Meta();
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var kv in meta)
        {
            if (kv.Value is null) continue;

            if (kv.Value is JsonValue v && v.TryGetValue<string>(out var s))
                dict[kv.Key] = s;
            else
                dict[kv.Key] = JsonVariableCodec.ToStableJson(kv.Value);
        }

        return dict;
    }

    // --------------------------------------------------------------------
    // Variables API (JSON blob)
    // --------------------------------------------------------------------
    public JsonNode? GetVariableNode(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        var k = NormalizeKey(key);

        var vars = Vars();
        return vars.TryGetPropertyValue(k, out var node) ? node : null;
    }

    public bool TryGetVariable<T>(string key, out T? value)
    {
        value = default;
        var node = GetVariableNode(key);
        if (node is null) return false;

        try
        {
            value = node.Deserialize<T>(JsonVariableCodec.Options);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void SetVariable(string name, object? value)
    {
        EnsureNotTerminal();

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Variable name cannot be empty.", nameof(name));

        var k = NormalizeKey(name);
        var vars = Vars();

        var node = JsonVariableCodec.ToNode(value ?? string.Empty);
        var newJson = JsonVariableCodec.ToStableJson(node);

        if (vars.TryGetPropertyValue(k, out var oldNode))
        {
            var oldJson = JsonVariableCodec.ToStableJson(oldNode);
            if (string.Equals(oldJson, newJson, StringComparison.Ordinal))
                return;
        }

        vars[k] = node;
        FlushVars(vars);

        AddDomainEvent(new UserTaskVariablesUpsertedDomainEvent(
            Id, ProcessId, TokenId, NodeInstanceId, ElementId,
            new Dictionary<string, object?>(StringComparer.Ordinal) { [k] = value ?? string.Empty },
            DateTime.UtcNow));
    }

    public void UpsertVariables(IReadOnlyDictionary<string, object?> values)
    {
        EnsureNotTerminal();
        if (values == null || values.Count == 0) return;

        var vars = Vars();
        var upsertsActual = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var kv in values)
        {
            var k = NormalizeKey(kv.Key);
            if (k.Length == 0) continue;

            var val = kv.Value ?? string.Empty;
            var node = JsonVariableCodec.ToNode(val);
            var newJson = JsonVariableCodec.ToStableJson(node);

            if (vars.TryGetPropertyValue(k, out var oldNode))
            {
                var oldJson = JsonVariableCodec.ToStableJson(oldNode);
                if (string.Equals(oldJson, newJson, StringComparison.Ordinal))
                    continue;
            }

            vars[k] = node;
            upsertsActual[k] = val;
        }

        if (upsertsActual.Count == 0)
            return;

        FlushVars(vars);

        AddDomainEvent(new UserTaskVariablesUpsertedDomainEvent(
            Id, ProcessId, TokenId, NodeInstanceId, ElementId,
            upsertsActual,
            DateTime.UtcNow));
    }

    // --------------------------------------------------------------------
    // Guards / Policies (unchanged logic)
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
        return UserTaskClaimMode.Claim;
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
        var assignee = GetMeta(UserTaskMeta.Assignee);
        if (!string.IsNullOrWhiteSpace(assignee))
        {
            if (!string.Equals(assignee, userId, StringComparison.Ordinal))
                throw new InvalidOperationException("Task is assigned to another user.");
            return;
        }

        var cu = SplitCsv(GetMeta(UserTaskMeta.CandidateUsers));
        if (cu.Count > 0 && cu.Contains(userId, StringComparer.Ordinal))
            return;

        var cg = SplitCsv(GetMeta(UserTaskMeta.CandidateGroups));
        if (cg.Count > 0)
            return;
    }

    // --------------------------------------------------------------------
    // Internals (Vars/Meta caches)
    // --------------------------------------------------------------------
    private JsonObject Vars()
    {
        if (_variablesLoaded && _variablesObj is not null)
            return _variablesObj;

        _variablesObj = JsonVariableCodec.ParseObjectOrEmpty(_variablesJson);
        _variablesLoaded = true;
        return _variablesObj;
    }

    private void FlushVars(JsonObject vars)
    {
        _variablesObj = vars;
        _variablesLoaded = true;
        _variablesJson = vars.ToJsonString(JsonVariableCodec.Options);
    }

    private JsonObject GetVarsClone()
    {
        var vars = Vars();
        var json = vars.ToJsonString(JsonVariableCodec.Options);
        return JsonVariableCodec.ParseObjectOrEmpty(json);
    }

    private JsonObject Meta()
    {
        if (_metadataLoaded && _metadataObj is not null)
            return _metadataObj;

        _metadataObj = JsonVariableCodec.ParseObjectOrEmpty(_metadataJson);
        _metadataLoaded = true;
        return _metadataObj;
    }

    private void FlushMeta(JsonObject meta)
    {
        _metadataObj = meta;
        _metadataLoaded = true;
        _metadataJson = meta.ToJsonString(JsonVariableCodec.Options);
    }

    private JsonObject GetMetaClone()
    {
        var meta = Meta();
        var json = meta.ToJsonString(JsonVariableCodec.Options);
        return JsonVariableCodec.ParseObjectOrEmpty(json);
    }

    // --------------------------------------------------------------------
    // Helpers
    // --------------------------------------------------------------------
    private static void EnsureRequired(Guid processId, Guid tokenId, string elementId, string taskName)
    {
        if (processId == Guid.Empty) throw new ArgumentException("ProcessId cannot be empty", nameof(processId));
        if (tokenId == Guid.Empty) throw new ArgumentException("TokenId cannot be empty", nameof(tokenId));
        if (string.IsNullOrWhiteSpace(elementId)) throw new ArgumentException("ElementId cannot be empty", nameof(elementId));
        if (string.IsNullOrWhiteSpace(taskName)) throw new ArgumentException("TaskName cannot be empty", nameof(taskName));
    }

    private static string NormalizeKey(string key) => (key ?? string.Empty).Trim();

    private static string EnsureKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key cannot be empty.", nameof(key));
        return key.Trim();
    }

    private static string ConvertToString(object? value)
    {
        if (value is null) return string.Empty;
        if (value is string s) return s;
        if (value is JsonNode n) return JsonVariableCodec.ToStableJson(n);
        return JsonSerializer.Serialize(value, JsonVariableCodec.Options);
    }

    private static string? JoinCsv(IEnumerable<string>? values)
    {
        if (values == null) return null;
        var list = values.Where(x => !string.IsNullOrWhiteSpace(x))
                         .Select(x => x.Trim())
                         .Distinct(StringComparer.Ordinal)
                         .ToList();
        return list.Count == 0 ? null : string.Join(",", list);
    }

    private static List<string> SplitCsv(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return new List<string>();
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
    IReadOnlyDictionary<string, object?>? Result) : IDomainEvent;

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
    IReadOnlyDictionary<string, object?> Upserts,
    DateTime OccurredAtUtc) : IDomainEvent;
