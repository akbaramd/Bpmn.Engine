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
    /// ID of the worker this token is waiting for (if any)
    /// </summary>
    public Guid? WorkerId { get; private set; }

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

        private readonly Dictionary<string, string> _variables = new();
        public IReadOnlyDictionary<string, string> Variables => _variables;

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

        }

        public void Wait(string? reason = null, Guid? workerId = null)
        {
            EnsureState(TokenState.Active);

            State = TokenState.Waiting;
            WorkerId = workerId;

            AddDomainEvent(new TokenWaitingEvent(
                TokenId: Id,
                ProcessId: ProcessId,
                ElementId: CurrentElementId,
                Reason: reason,
                WorkerId: workerId,
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
                IsExecutable: IsExecutable,
                ScopeId: ScopeId));
        }
        public void Processed()
        {
            EnsureState(TokenState.Active);
            WorkerId = null;
            AddDomainEvent(new TokenProcessedEvent(
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
        public void ClearLocalVariables()
        {
            EnsureNotTerminal();
            if (_variables.Count == 0)
                return;

            var clearedCount = _variables.Count;
            _variables.Clear();

            AddDomainEvent(new TokenLocalVariablesClearedEvent(
                Id,
                ProcessId,
                clearedCount,
                DateTime.UtcNow));
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
        public void SetVariable(string name, object? value)
        {
            EnsureNotTerminal();

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Variable name cannot be empty", nameof(name));

            _variables[name] = ConvertToString(value);

            AddDomainEvent(new TokenLocalVariableSetEvent(
                Id,
                ProcessId,
                name,
                DateTime.UtcNow));
        }

        public bool TryGetVariable(string name, out string? value)
        {
            value = null;
            if (string.IsNullOrWhiteSpace(name)) return false;
            return _variables.TryGetValue(name, out value);
        }

        public string GetVariable(string name)
        {
            if (!_variables.TryGetValue(name, out var value))
                throw new KeyNotFoundException($"Variable '{name}' not found.");

            return value;
        }

        /// <summary>
        /// Converts an object to string representation
        /// </summary>
        private static string ConvertToString(object? value)
        {
            if (value == null)
                return string.Empty;

            if (value is string str)
                return str;

            // Use JSON serialization for complex types
            return Newtonsoft.Json.JsonConvert.SerializeObject(value);
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
