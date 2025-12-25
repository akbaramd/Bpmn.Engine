using Novin.Bpmn.Engine.Domain.Common;
using Novin.Bpmn.Engine.Domain.Events;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using System;

namespace Novin.Bpmn.Engine.Domain.Entities
{
    public sealed class Token : BaseAggregateRoot
    {
        public Guid ProcessId { get; private set; }
        public string CurrentElementId { get; private set; } = default!;
        public TokenState State { get; private set; }

        /// <summary>
        /// If false => bypass-only token, never executes activities (only moves)
        /// </summary>
        public bool IsExecutable { get; private set; } = true;

        public Guid? ScopeId { get; private set; }
        public string? ArrivedViaFlowId { get; private set; }
        
        /// <summary>
        /// Activity Instance ID - برای cancel کردن activity instance در interrupting boundary events
        /// این با ScopeId متفاوت است: ScopeId برای fork/join correlation است،
        /// ActivityInstanceId برای شناسایی تمام tokenهای داخل یک activity instance (مثل subprocess)
        /// </summary>
        public Guid? ActivityInstanceId { get; private set; }

        private readonly List<Guid> _parentTokenIds = new();
        public IReadOnlyCollection<Guid> ParentTokenIds => _parentTokenIds.AsReadOnly();

        private readonly Dictionary<string, object> _variables = new();
        public IReadOnlyDictionary<string, object> Variables => _variables;

        public DateTime CreatedAt { get; private set; }
        public DateTime? ActivatedAt { get; private set; }
        public DateTime? CompletedAt { get; private set; }

        private Token()
        {
            State = TokenState.Created;
            CreatedAt = DateTime.UtcNow;
        }

        public Token(Guid processId, string startElementId, IEnumerable<Guid>? parentTokenIds = null)
            : this()
        {
            if (processId == Guid.Empty)
                throw new ArgumentException("ProcessId cannot be empty", nameof(processId));

            if (string.IsNullOrWhiteSpace(startElementId))
                throw new ArgumentException("Start element cannot be empty", nameof(startElementId));

            ProcessId = processId;
            CurrentElementId = startElementId;

            if (parentTokenIds != null)
                _parentTokenIds.AddRange(parentTokenIds.Where(x => x != Guid.Empty).Distinct());

            AddDomainEvent(new TokenCreatedEvent(
                TokenId: Id,
                ProcessId: ProcessId,
                StartElementId: startElementId,
                ParentTokenIds: _parentTokenIds.AsReadOnly(),
                OccurredAtUtc: CreatedAt));
        }

        // -------------------- Lifecycle --------------------
        public void Activate()
        {
            EnsureState(TokenState.Created);

            State = TokenState.Active;
            ActivatedAt = DateTime.UtcNow;

            AddDomainEvent(new TokenActivatedEvent(
                TokenId: Id,
                ProcessId: ProcessId,
                ElementId: CurrentElementId,
                OccurredAtUtc: DateTime.UtcNow,
                IsExecutable: IsExecutable));

            RequestProcessing();
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
                IsExecutable: IsExecutable,
                ScopeId: ScopeId));
        }
        public void SetArrivedVia(string? flowId)
        {
            if (string.IsNullOrWhiteSpace(flowId))
                throw new ArgumentException("FlowId cannot be empty or null", nameof(flowId));

            ArrivedViaFlowId = flowId;

            // ایجاد رویداد برای ثبت رسیدن توکن از طریق جریان مشخص
            AddDomainEvent(new TokenArrivedViaFlowEvent(
                TokenId: Id,
                ProcessId: ProcessId,
                ElementId: CurrentElementId,
                ArrivedViaFlowId: flowId,
                OccurredAtUtc: DateTime.UtcNow,
                IsExecutable: IsExecutable,
                ScopeId: ScopeId));
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
                IsExecutable: IsExecutable,
                ScopeId: ScopeId));

            RequestProcessing();
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
                IsExecutable: IsExecutable,
                ScopeId: ScopeId));

            RequestProcessing();
        }

        public void Complete()
        {
            EnsureState(TokenState.Active);

            State = TokenState.Completed;
            CompletedAt = DateTime.UtcNow;

            AddDomainEvent(new TokenCompletedEvent(
                TokenId: Id,
                ProcessId: ProcessId,
                ElementId: CurrentElementId,
                OccurredAtUtc: DateTime.UtcNow,
                IsExecutable: IsExecutable,
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
                IsExecutable: IsExecutable,
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
                IsExecutable: IsExecutable,
                ScopeId: ScopeId));
        }
        public void ClearLocalVariables()
        {
            EnsureNotTerminal();
            _variables.Clear();
        }
        // -------------------- Movement --------------------
        public void MoveTo(string nextElementId, string? viaFlowId)
        {
            EnsureState(TokenState.Active);

            if (string.IsNullOrWhiteSpace(nextElementId))
                throw new ArgumentException("Next element id cannot be empty", nameof(nextElementId));

            var from = CurrentElementId;
            CurrentElementId = nextElementId;
            ArrivedViaFlowId = viaFlowId;

            AddDomainEvent(new TokenMovedEvent(
                TokenId: Id,
                ProcessId: ProcessId,
                FromElementId: from,
                ToElementId: nextElementId,
                ViaFlowId: viaFlowId,
                OccurredAtUtc: DateTime.UtcNow,
                IsExecutable: IsExecutable,
                ScopeId: ScopeId));

            RequestProcessing();
        }
        public void ResumeWithoutProcessing()
        {
            EnsureState(TokenState.Waiting);

            State = TokenState.Active;

            AddDomainEvent(new TokenResumedEvent(
                TokenId: Id,
                ProcessId: ProcessId,
                ElementId: CurrentElementId,
                OccurredAtUtc: DateTime.UtcNow,
                IsExecutable: IsExecutable,
                ScopeId: ScopeId));

            // ❌ عمداً RequestProcessing نمی‌زنیم
        }
        private void RequestProcessing()
        {
            AddDomainEvent(new TokenProcessingRequestedEvent(
                TokenId: Id,
                ProcessId: ProcessId,
                ElementId: CurrentElementId,
                OccurredAtUtc: DateTime.UtcNow,
                IsExecutable: IsExecutable,
                ScopeId: ScopeId,
                ArrivedViaFlowId: ArrivedViaFlowId));
        }

        // -------------------- Fork/Merge correlation --------------------
        public void MarkNonExecutable(string? reason = null)
        {
            if (!IsExecutable) return;

            IsExecutable = false;

            AddDomainEvent(new TokenBecameNonExecutableEvent(
                TokenId: Id,
                ProcessId: ProcessId,
                ElementId: CurrentElementId,
                OccurredAtUtc: DateTime.UtcNow,
                ScopeId: ScopeId));
        }

        public void SetScope(Guid scopeId)
        {
            if (scopeId == Guid.Empty)
                throw new ArgumentException("ScopeId cannot be empty", nameof(scopeId));

            ScopeId = scopeId;

            AddDomainEvent(new TokenScopeAssignedEvent(
                TokenId: Id,
                ProcessId: ProcessId,
                ScopeId: scopeId,
                OccurredAtUtc: DateTime.UtcNow));
        }

        public void ClearScope() => ScopeId = null;

        public void ClearArrivedVia() => ArrivedViaFlowId = null;
        
        /// <summary>
        /// Set Activity Instance ID - وقتی token وارد یک activity می‌شود که scope جدید ایجاد می‌کند
        /// (مثل UserTask, SubProcess, ...)
        /// </summary>
        public void SetActivityInstance(Guid activityInstanceId)
        {
            if (activityInstanceId == Guid.Empty)
                throw new ArgumentException("ActivityInstanceId cannot be empty", nameof(activityInstanceId));

            ActivityInstanceId = activityInstanceId;
        }
        
        /// <summary>
        /// Clear Activity Instance ID - وقتی token از activity خارج می‌شود
        /// </summary>
        public void ClearActivityInstance() => ActivityInstanceId = null;

        // -------------------- Variables --------------------
        public void SetVariable(string name, object value)
        {
            EnsureNotTerminal();

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Variable name cannot be empty", nameof(name));

            _variables[name] = value;
        }

        public bool TryGetVariable(string name, out object? value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(name)) return false;
            return _variables.TryGetValue(name, out value);
        }

        public object GetVariable(string name)
        {
            if (!_variables.TryGetValue(name, out var value))
                throw new KeyNotFoundException($"Variable '{name}' not found.");

            return value;
        }

        public bool HasVariable(string name) => _variables.ContainsKey(name);

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
