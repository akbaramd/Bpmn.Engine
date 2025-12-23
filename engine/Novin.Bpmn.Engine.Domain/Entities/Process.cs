using Novin.Bpmn.Engine.Domain.Common;
using Novin.Bpmn.Engine.Domain.Events;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Domain.Entities;

/// <summary>
/// Aggregate root representing a BPMN process instance
/// </summary>
public class Process : BaseAggregateRoot
{
    public string Name { get; private set; }
    public string ProcessDefinitionId { get; private set; }
    public ProcessState State { get; private set; }
    public Dictionary<string, object> Variables { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    
    // References to other aggregates (by ID only, following DDD principles)
    private readonly List<Guid> _tokenIds = new();
    public IReadOnlyCollection<Guid> TokenIds => _tokenIds.AsReadOnly();
    
    
    // History of executed nodes in this process
    private readonly List<ProcessHistory> _history = new();
    public IReadOnlyCollection<ProcessHistory> History => _history.AsReadOnly();

    private Process() : base()
    {
        Variables = new Dictionary<string, object>();
        State = ProcessState.Created;
        CreatedAt = DateTime.UtcNow;
    }

    public Process(string name, string processDefinitionId, Dictionary<string, object>? initialVariables = null) : this()
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Process name cannot be null or empty", nameof(name));
        
        if (string.IsNullOrWhiteSpace(processDefinitionId))
            throw new ArgumentException("Process definition ID cannot be null or empty", nameof(processDefinitionId));

        Name = name;
        ProcessDefinitionId = processDefinitionId;
        Variables = initialVariables ?? new Dictionary<string, object>();
        
        AddDomainEvent(new ProcessCreatedEvent(Id, Name, ProcessDefinitionId, CreatedAt));
    }

    public void Start()
    {
        if (State != ProcessState.Created)
            throw new InvalidOperationException($"Cannot start process in {State} state. Process must be in Created state.");

        State = ProcessState.Running;
        StartedAt = DateTime.UtcNow;
        
        AddDomainEvent(new ProcessStartedEvent(Id, StartedAt.Value));
    }

    public void Complete()
    {
        if (State != ProcessState.Running)
            throw new InvalidOperationException($"Cannot complete process in {State} state. Process must be in Running state.");

        State = ProcessState.Completed;
        CompletedAt = DateTime.UtcNow;
        
        AddDomainEvent(new ProcessCompletedEvent(Id, CompletedAt.Value));
    }

    public void Suspend()
    {
        if (State != ProcessState.Running)
            throw new InvalidOperationException($"Cannot suspend process in {State} state. Process must be in Running state.");

        State = ProcessState.Suspended;
        
        AddDomainEvent(new ProcessSuspendedEvent(Id, DateTime.UtcNow));
    }

    public void Resume()
    {
        if (State != ProcessState.Suspended)
            throw new InvalidOperationException($"Cannot resume process in {State} state. Process must be in Suspended state.");

        State = ProcessState.Running;
        
        AddDomainEvent(new ProcessResumedEvent(Id, DateTime.UtcNow));
    }

    public void Terminate(string? reason = null)
    {
        if (State == ProcessState.Completed || State == ProcessState.Terminated)
            throw new InvalidOperationException($"Cannot terminate process in {State} state.");

        State = ProcessState.Terminated;
        
        AddDomainEvent(new ProcessTerminatedEvent(Id, DateTime.UtcNow, reason));
    }

    public void Fail(string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
            throw new ArgumentException("Error message cannot be null or empty", nameof(errorMessage));

        State = ProcessState.Failed;
        
        AddDomainEvent(new ProcessFailedEvent(Id, DateTime.UtcNow, errorMessage));
    }

    public void SetVariable(string key, object value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Variable key cannot be null or empty", nameof(key));

        Variables[key] = value;
        
        AddDomainEvent(new ProcessVariableUpdatedEvent(Id, key, value, DateTime.UtcNow));
    }

   
    /// <summary>
    /// Adds a token to the process
    /// </summary>
    public void AddToken(Guid tokenId)
    {
        if (tokenId == Guid.Empty)
            throw new ArgumentException("Token ID cannot be empty", nameof(tokenId));

        if (_tokenIds.Contains(tokenId))
            throw new InvalidOperationException($"Token {tokenId} is already part of this process.");

        _tokenIds.Add(tokenId);
    }

    /// <summary>
    /// Removes a token from the process
    /// </summary>
    public void RemoveToken(Guid tokenId)
    {
        if (!_tokenIds.Remove(tokenId))
            throw new InvalidOperationException($"Token {tokenId} is not part of this process.");
    }

    /// <summary>
    /// Records a node execution in the process history
    /// </summary>
    public void RecordNodeExecution(Guid nodeId, string elementId, string nodeName, NodeState state, Guid? tokenId = null)
    {
        if (nodeId == Guid.Empty)
            throw new ArgumentException("Node ID cannot be empty", nameof(nodeId));

        if (string.IsNullOrWhiteSpace(elementId))
            throw new ArgumentException("Element ID cannot be null or empty", nameof(elementId));

        if (string.IsNullOrWhiteSpace(nodeName))
            throw new ArgumentException("Node name cannot be null or empty", nameof(nodeName));

        var historyEntry = new ProcessHistory(
            Id,
            nodeId,
            elementId,
            nodeName,
            state,
            tokenId);

        _history.Add(historyEntry);
    }

    /// <summary>
    /// Records that a token has reached a node in the token's history
    /// This method coordinates between Process, Token, and Node aggregates
    /// Only callable from within the Domain layer (aggregate roots)
    /// </summary>
    public void RecordTokenNodeReached(Token token, Node node, Dictionary<string, object>? variables = null)
    {
        if (token == null)
            throw new ArgumentNullException(nameof(token));

        if (node == null)
            throw new ArgumentNullException(nameof(node));

        if (token.ProcessId != Id)
            throw new InvalidOperationException($"Token {token.Id} does not belong to process {Id}.");

        if (node.ProcessId != Id)
            throw new InvalidOperationException($"Node {node.Id} does not belong to process {Id}.");

        // Call internal method on Token to add history entry
        // This is allowed since both are in the same Domain assembly
        token.AddHistoryEntry(node.Id, node.ElementId, node.NodeName, token.State, variables);
    }

    /// <summary>
    /// Gets the execution history for a specific node
    /// </summary>
    public IEnumerable<ProcessHistory> GetNodeExecutionHistory(Guid nodeId)
    {
        return _history.Where(e => e.NodeId == nodeId);
    }

    /// <summary>
    /// Gets the execution history for a specific element
    /// </summary>
    public IEnumerable<ProcessHistory> GetElementExecutionHistory(string elementId)
    {
        return _history.Where(e => e.ElementId == elementId);
    }

    /// <summary>
    /// Gets all history entries
    /// </summary>
    public IEnumerable<ProcessHistory> GetAllHistory()
    {
        return _history.AsEnumerable();
    }

    /// <summary>
    /// Checks if all tokens are completed
    /// </summary>
    public bool AreAllTokensCompleted()
    {
        // This would need to check token states through repository
        // For now, we'll assume process completion is handled externally
        return false;
    }
}

