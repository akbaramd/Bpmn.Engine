using Novin.Bpmn.Engine.Domain.Common;
using Novin.Bpmn.Engine.Domain.Events;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Novin.Bpmn.Engine.Domain.Entities
{
    public sealed class Token : BaseAggregateRoot
    {
        public Guid ProcessId { get; private set; }
        public string CurrentElementId { get; private set; } = default!;
        public TokenState State { get; private set; }

        // -------------------- Scope (Zeebe-like) --------------------


        // ✅ nested scopes support
        private readonly List<Guid> _scopeStack = new();
        public IReadOnlyList<Guid> ScopeStack => _scopeStack.AsReadOnly();

        // ✅ persisted current scope for EF + indexing
        public Guid? ScopeId { get; private set; }

        // helpful for debugging / guards
        public Guid? ParentScopeId => _scopeStack.Count < 2 ? null : _scopeStack[^2];


        private readonly List<string> _arrivedViaFlowIds = new();
        public IReadOnlyList<string> ArrivedViaFlowIds => _arrivedViaFlowIds.AsReadOnly();

        /// <summary>
        /// Activity Instance ID - برای cancel کردن activity instance در interrupting boundary events
        /// این با ScopeId متفاوت است: ScopeId برای fork/join correlation است،
        /// ActivityInstanceId برای شناسایی تمام tokenهای داخل یک activity instance (مثل subprocess)
        /// </summary>
        public Guid? ActivityInstanceId { get; private set; }

        /// <summary>
        /// Parent Token ID - برای Fork/Join correlation
        /// هر child token فقط یک parent دارد
        /// </summary>
        public Guid? ParentTokenId { get; private set; }

 // ✅ JSON-native local variables (Zeebe-like “document variables”)
        private readonly Dictionary<string, JsonNode?> _variables = new(StringComparer.Ordinal);
        public IReadOnlyDictionary<string, JsonNode?> Variables => _variables;

        public void ApplyVariablesPatch(VariablesPatch patch)
        {
            EnsureNotTerminal();
            if (patch is null || !patch.HasChanges) return;

            // removals first
            if (patch.Removals is not null)
            {
                foreach (var k in patch.Removals)
                    RemoveVariable(k);
            }

            if (patch.Upserts is not null)
            {
                foreach (var kv in patch.Upserts)
                    SetVariable(kv.Key, kv.Value);
            }
        }

        public void SetVariable(string name, object? value)
        {
            EnsureNotTerminal();

            var key = VariablesPatch.NormalizeKey(name);
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Variable name cannot be empty", nameof(name));

            // null => remove
            if (value is null)
            {
                RemoveVariable(key);
                return;
            }

            var node = value is JsonNode jn ? jn : JsonVariableCodec.ToNode(value);
            if (node is null)
            {
                RemoveVariable(key);
                return;
            }

            // clone to detach from external references
            var newNode = JsonVariableCodec.CloneNode(node);
            var newJson = JsonVariableCodec.ToStableJson(newNode);

            if (_variables.TryGetValue(key, out var oldNode))
            {
                var oldJson = JsonVariableCodec.ToStableJson(oldNode);
                if (string.Equals(oldJson, newJson, StringComparison.Ordinal))
                    return;
            }

            _variables[key] = newNode;

            AddDomainEvent(new TokenLocalVariableSetEvent(
                Id,
                ProcessId,
                key,
                DateTime.UtcNow));
        }

        public bool RemoveVariable(string name)
        {
            EnsureNotTerminal();

            var key = VariablesPatch.NormalizeKey(name);
            if (string.IsNullOrWhiteSpace(key)) return false;

            return _variables.Remove(key);
        }

        public bool HasVariable(string name)
        {
            var key = VariablesPatch.NormalizeKey(name);
            return !string.IsNullOrWhiteSpace(key) && _variables.ContainsKey(key);
        }

        public JsonNode? GetVariableNode(string name)
        {
            var key = VariablesPatch.NormalizeKey(name);
            if (string.IsNullOrWhiteSpace(key)) return null;
            return _variables.TryGetValue(key, out var node) ? node : null;
        }

        public string? GetVariableJson(string name)
        {
            var node = GetVariableNode(name);
            return node is null ? null : JsonVariableCodec.ToStableJson(node);
        }

        public bool TryGetVariable<T>(string name, out T? value)
        {
            value = default;

            var node = GetVariableNode(name);
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

        public T? GetVariable<T>(string name)
        {
            var node = GetVariableNode(name);
            if (node is null) return default;
            return node.Deserialize<T>(JsonVariableCodec.Options);
        }

        public void ClearLocalVariables()
        {
            EnsureNotTerminal();
            if (_variables.Count == 0) return;

            var clearedCount = _variables.Count;
            _variables.Clear();

            AddDomainEvent(new TokenLocalVariablesClearedEvent(
                Id,
                ProcessId,
                clearedCount,
                DateTime.UtcNow));
        }

        public DateTime CreatedAt { get; private set; }
        public DateTime? ActivatedAt { get; private set; }
        public DateTime? CompletedAt { get; private set; }

        private Token()
        {
            State = TokenState.Created;
            CreatedAt = DateTime.UtcNow;
        }

        public Token(Guid processId, string startElementId, Guid? parentTokenId = null)
            : this()
        {
            if (processId == Guid.Empty)
                throw new ArgumentException("ProcessId cannot be empty", nameof(processId));

            if (string.IsNullOrWhiteSpace(startElementId))
                throw new ArgumentException("Start element cannot be empty", nameof(startElementId));

            ProcessId = processId;
            CurrentElementId = startElementId;

            if (parentTokenId.HasValue && parentTokenId.Value != Guid.Empty)
                ParentTokenId = parentTokenId.Value;

            AddDomainEvent(new TokenCreatedEvent(
                TokenId: Id,
                ProcessId: ProcessId,
                StartElementId: startElementId,
                ParentTokenId: ParentTokenId,
                OccurredAtUtc: CreatedAt));
        }

        // -------------------- Lifecycle --------------------
        public void Activate()
        {

            State = TokenState.Active;
            ActivatedAt = DateTime.UtcNow;

            AddDomainEvent(new TokenActivatedEvent(
                TokenId: Id,
                ProcessId: ProcessId,
                ElementId: CurrentElementId,
                OccurredAtUtc: DateTime.UtcNow));

        }

        public void Wait(string? reason = null)
        {
            EnsureState(TokenState.Active);

            State = TokenState.Waiting;

            AddDomainEvent(new TokenWaitingEvent(
                TokenId: Id,
                ProcessId: ProcessId,
                ElementId: CurrentElementId,
                Reason: reason,
                OccurredAtUtc: DateTime.UtcNow,
                ScopeId: ScopeId));
        }
        public void SetArrivedVia(string? flowId)
        {
            if (string.IsNullOrWhiteSpace(flowId))
                throw new ArgumentException("FlowId cannot be empty or null", nameof(flowId));

            if (!_arrivedViaFlowIds.Contains(flowId, StringComparer.Ordinal))
            {
                _arrivedViaFlowIds.Add(flowId);
            }

            // ایجاد رویداد برای ثبت رسیدن توکن از طریق جریان مشخص
            AddDomainEvent(new TokenArrivedViaFlowEvent(
                TokenId: Id,
                ProcessId: ProcessId,
                ElementId: CurrentElementId,
                ArrivedViaFlowId: flowId,
                OccurredAtUtc: DateTime.UtcNow,
                ScopeId: ScopeId));
        }

        public void SetArrivedViaFlowIds(IEnumerable<string>? flowIds)
        {
            _arrivedViaFlowIds.Clear();
            if (flowIds != null)
            {
                foreach (var flowId in flowIds)
                {
                    if (!string.IsNullOrWhiteSpace(flowId) && !_arrivedViaFlowIds.Contains(flowId, StringComparer.Ordinal))
                    {
                        _arrivedViaFlowIds.Add(flowId);
                    }
                }
            }
        }
        public void Resume()
        {
            EnsureState(TokenState.Waiting);

            State = TokenState.Active;

            AddDomainEvent(new TokenResumedEvent(
                TokenId: Id,
                ProcessId: ProcessId,
                ElementId: CurrentElementId,
                OccurredAtUtc: DateTime.UtcNow,
                ScopeId: ScopeId));

        }

        /// <summary>
        /// Retry a failed token: convert from Failed to Active and request processing
        /// </summary>
        public void Retry()
        {
            if (State != TokenState.Failed)
                throw new InvalidOperationException($"Cannot retry token in {State} state. Token must be Failed.");

            State = TokenState.Active;

            AddDomainEvent(new TokenRetriedEvent(
                TokenId: Id,
                ProcessId: ProcessId,
                ElementId: CurrentElementId,
                OccurredAtUtc: DateTime.UtcNow,
                ScopeId: ScopeId));

        }
        public void Complete(string? reason = null)
        {
            EnsureState(TokenState.Active);

            State = TokenState.Completed;
            CompletedAt = DateTime.UtcNow;

            AddDomainEvent(new TokenCompletedEvent(
                TokenId: Id,
                ProcessId: ProcessId,
                ElementId: CurrentElementId,
                Reason: reason,
                OccurredAtUtc: CompletedAt.Value,
                ScopeId: ScopeId));
        }
        public void Processed()
        {
            ClearActivityInstance();
            EnsureState(TokenState.Active);
            AddDomainEvent(new TokenProcessedEvent(
                TokenId: Id,
                ProcessId: ProcessId,
                ElementId: CurrentElementId,
                OccurredAtUtc: DateTime.UtcNow,
                ScopeId: ScopeId));
        }

        /// <summary>
        /// Fail token with a technical failure (default)
        /// </summary>
        public void Fail(string error)
        {
            Fail(error, ErrorType.TechnicalFailure, errorCode: null, incidentId: null);
        }

        /// <summary>
        /// Fail token with specific error type and optional incident
        /// </summary>
        public void Fail(
            string error,
            ErrorType errorType,
            string? errorCode = null,
            Guid? incidentId = null)
        {
            if (string.IsNullOrWhiteSpace(error))
                throw new ArgumentException("Error cannot be empty", nameof(error));

            if (State is TokenState.Completed or TokenState.Terminated)
                throw new InvalidOperationException($"Cannot fail token in {State} state.");

            State = TokenState.Failed;

            AddDomainEvent(new TokenFailedEvent(
                TokenId: Id,
                ProcessId: ProcessId,
                ElementId: CurrentElementId,
                Error: error,
                OccurredAtUtc: DateTime.UtcNow,
                ScopeId: ScopeId,
                IncidentId: incidentId,
                ErrorType: errorType.ToString(),
                ErrorCode: errorCode));
        }

        public void Terminate(string? reason = null)
        {
            if (State == TokenState.Completed)
                throw new InvalidOperationException("Completed token cannot be terminated.");

            State = TokenState.Terminated;

            AddDomainEvent(new TokenTerminatedEvent(
                TokenId: Id,
                ProcessId: ProcessId,
                ElementId: CurrentElementId,
                Reason: reason,
                OccurredAtUtc: DateTime.UtcNow,
                ScopeId: ScopeId));
        }

        /// <summary>
        /// Mark token as Forked when it creates child tokens at a split gateway
        /// </summary>
        public void Fork(int childCount, string? reason = null)
        {
            EnsureState(TokenState.Active);


            State = TokenState.Forked;

            AddDomainEvent(new TokenForkedEvent(
                TokenId: Id,
                ProcessId: ProcessId,
                ElementId: CurrentElementId,
                ScopeId: ScopeId ?? Guid.Empty,
                ChildCount: childCount,
                OccurredAtUtc: DateTime.UtcNow));
        }

        /// <summary>
        /// Mark token as Merged when it arrives at a join gateway
        /// </summary>
        public void Merge(Guid parentTokenId, string? reason = null)
        {
            if (State is TokenState.Completed or TokenState.Failed)
                throw new InvalidOperationException($"Cannot merge token in {State} state.");

            if (parentTokenId == Guid.Empty)
                throw new ArgumentException("ParentTokenId cannot be empty", nameof(parentTokenId));

            State = TokenState.Merged;

            AddDomainEvent(new TokenMergedEvent(
                TokenId: Id,
                ProcessId: ProcessId,
                ElementId: CurrentElementId,
                ScopeId: ScopeId ?? Guid.Empty,
                ParentTokenId: parentTokenId,
                OccurredAtUtc: DateTime.UtcNow));
        }

        /// <summary>
        /// Reactivate token from Forked state when all children have merged
        /// </summary>
        public void ReactivateFromForked(int mergedChildCount, string? reason = null)
        {
            EnsureState(TokenState.Forked);

            var scopeId = ScopeId ?? Guid.Empty;
            State = TokenState.Active;

            AddDomainEvent(new TokenReactivatedFromForkedEvent(
                TokenId: Id,
                ProcessId: ProcessId,
                ElementId: CurrentElementId,
                ScopeId: scopeId,
                MergedChildCount: mergedChildCount,
                OccurredAtUtc: DateTime.UtcNow));
        }

        /// <summary>
        /// Reports that BPMN error occurred during token processing
        /// </summary>
        public void ReportBpmnError(string errorCode, string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(errorCode))
                throw new ArgumentException("Error code cannot be empty", nameof(errorCode));

            AddDomainEvent(new BpmnErrorOccurredEvent(
                TokenId: Id,
                ProcessId: ProcessId,
                ElementId: CurrentElementId,
                ErrorCode: errorCode,
                ErrorMessage: errorMessage,
                ScopeId: ScopeId,
                OccurredAtUtc: DateTime.UtcNow));
        }

        /// <summary>
        /// Reports that technical failure occurred during token processing
        /// </summary>
        public void ReportTechnicalFailure(string errorMessage, string stackTrace)
        {
            if (string.IsNullOrWhiteSpace(errorMessage))
                throw new ArgumentException("Error message cannot be empty", nameof(errorMessage));

            AddDomainEvent(new TechnicalFailureOccurredEvent(
                TokenId: Id,
                ProcessId: ProcessId,
                ElementId: CurrentElementId,
                ErrorMessage: errorMessage,
                StackTrace: stackTrace,
                OccurredAtUtc: DateTime.UtcNow));
        }
       
        // -------------------- Movement --------------------
        public void MoveTo(string nextElementId, bool skipProcess = false, params string?[] viaFlowId)
        {
            EnsureState(TokenState.Active);

            if (string.IsNullOrWhiteSpace(nextElementId))
                throw new ArgumentException("Next element id cannot be empty", nameof(nextElementId));

            var from = CurrentElementId;
            var activityInstanceId = ActivityInstanceId; // 🔴 snapshot قبل از تغییر

            CurrentElementId = nextElementId;

            // Clear previous flow IDs and set only the current flow ID for this move
            _arrivedViaFlowIds.Clear();
            if (viaFlowId.Any())
            {
                _arrivedViaFlowIds.AddRange(viaFlowId!);
            }

            AddDomainEvent(new TokenMovedEvent(
                TokenId: Id,
                ProcessId: ProcessId,
                FromElementId: from,
                ToElementId: nextElementId,
                ViaFlowIds: _arrivedViaFlowIds,
                OccurredAtUtc: DateTime.UtcNow,
                ScopeId: ScopeId,
                SkipProcess: skipProcess,
                ActivityInstanceId: activityInstanceId
            ));
        }

        public void ResumeWithoutProcessing()
        {
            EnsureState(TokenState.Waiting);

            State = TokenState.Active;

            // ❌ عمداً RequestProcessing نمی‌زنیم
        }

        public void ReActivate()
        {
            State = TokenState.Active;
        }


        // -------------------- Scope Stack API --------------------

        // ✅ for split gateway: push a new scope
        public void PushScope(Guid scopeId)
        {
            if (scopeId == Guid.Empty)
                throw new ArgumentException("ScopeId cannot be empty", nameof(scopeId));

            _scopeStack.Add(scopeId);
            ScopeId = scopeId; // ✅ sync current scope

            AddDomainEvent(new TokenScopeAssignedEvent(
                TokenId: Id,
                ProcessId: ProcessId,
                ScopeId: scopeId,
                OccurredAtUtc: DateTime.UtcNow));
        }

        // ✅ for join completion: return to parent scope
        public Guid? PopScope()
        {
            if (_scopeStack.Count == 0)
                return null;

            _scopeStack.RemoveAt(_scopeStack.Count - 1);

            ScopeId = _scopeStack.Count == 0 ? null : _scopeStack[^1]; // ✅ sync
            return ScopeId;
        }

        public void ClearAllScopes()
        {
            _scopeStack.Clear();
            ScopeId = null; // ✅ sync
        }

        // backward compatible alias
        public void SetScope(Guid scopeId) => PushScope(scopeId);

        // ⚠️ old name; now means clear all
        public void ClearScope() => ClearAllScopes();

        public void SetScopeStackSnapshot(IEnumerable<Guid> scopes)
        {
            if (scopes is null) throw new ArgumentNullException(nameof(scopes));

            _scopeStack.Clear();
            foreach (var s in scopes)
            {
                if (s != Guid.Empty)
                    _scopeStack.Add(s);
            }

            ScopeId = _scopeStack.Count == 0 ? null : _scopeStack[^1]; // ✅ sync
        }

        /// <summary>
        /// ✅ EF-friendly replace with guard + normalization
        /// </summary>
        public void ReplaceScopeStack(IReadOnlyList<Guid> scopeStackSnapshot)
        {
            if (scopeStackSnapshot is null)
                throw new ArgumentNullException(nameof(scopeStackSnapshot));

            _scopeStack.Clear();

            for (int i = 0; i < scopeStackSnapshot.Count; i++)
            {
                var s = scopeStackSnapshot[i];
                if (s != Guid.Empty)
                    _scopeStack.Add(s);
            }

            ScopeId = _scopeStack.Count == 0 ? null : _scopeStack[^1]; // ✅ sync
        }

        public void ClearArrivedVia() => _arrivedViaFlowIds.Clear();

        /// <summary>
        /// Set Activity Instance ID - وقتی token وارد یک activity می‌شود که scope جدید ایجاد می‌کند
        /// (مثل UserTask, SubProcess, ...)
        /// </summary>
        public void SetActivityInstance(Guid activityInstanceId)
        {
            if (activityInstanceId == Guid.Empty)
                throw new ArgumentException("ActivityInstanceId cannot be empty", nameof(activityInstanceId));

            ActivityInstanceId = activityInstanceId;

            AddDomainEvent(new TokenActivityInstanceAssignedEvent(
                Id,
                ProcessId,
                activityInstanceId,
                DateTime.UtcNow));
        }

        /// <summary>
        /// Clear Activity Instance ID - وقتی token از activity خارج می‌شود
        /// </summary>
        public void ClearActivityInstance()
        {
            var previous = ActivityInstanceId;
            ActivityInstanceId = null;

            AddDomainEvent(new TokenActivityInstanceClearedEvent(
                Id,
                ProcessId,
                previous,
                DateTime.UtcNow));
        }

        // -------------------- Variables --------------------
       

        // -------------------- Guards --------------------
        private void EnsureState(TokenState required)
        {
            if (State != required)
                throw new InvalidOperationException($"Token must be in {required} state but is {State}.");
        }

        private void EnsureNotTerminal()
        {
            if (State is TokenState.Completed or TokenState.Terminated)
                throw new InvalidOperationException($"Token is terminal: {State}");
        }



    }
}
