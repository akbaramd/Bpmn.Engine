using Novin.Bpmn.Engine.Domain.Common;
using Novin.Bpmn.Engine.Domain.Events;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Domain.Entities;

/// <summary>
/// Aggregate root representing a BPMN node (Task, Gateway, Event, etc.)
/// </summary>
public class Node : BaseAggregateRoot
{
    public Guid ProcessId { get; private set; }
    public string NodeName { get; private set; }
    public string ElementId { get; private set; }
    public NodeType Type { get; private set; }
    public NodeState State { get; private set; }
    public Dictionary<string, object> Variables { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ProcessingStartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public DateTime? FailedAt { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    
    // Current tokens in this node (by ID only, following DDD principles)
    private readonly List<Guid> _currentTokenIds = new();
    public IReadOnlyCollection<Guid> CurrentTokenIds => _currentTokenIds.AsReadOnly();
    
    // History of tokens that passed through this node (Child Entities)
    private readonly List<NodeTokenHistoryEntry> _tokenHistory = new();
    public IReadOnlyCollection<NodeTokenHistoryEntry> TokenHistory => _tokenHistory.AsReadOnly();
    
    // Parent nodes (nodes that come before this node - for Join/Merge scenarios)
    private readonly List<Guid> _parentNodeIds = new();
    public IReadOnlyCollection<Guid> ParentNodeIds => _parentNodeIds.AsReadOnly();
    
    // Child nodes (nodes that come after this node - for Fork scenarios)
    private readonly List<Guid> _childNodeIds = new();
    public IReadOnlyCollection<Guid> ChildNodeIds => _childNodeIds.AsReadOnly();
    
    private readonly List<string> _history = new();
    public IReadOnlyCollection<string> History => _history.AsReadOnly();

    private Node() : base()
    {
        Variables = new Dictionary<string, object>();
        State = NodeState.Pending;
        CreatedAt = DateTime.UtcNow;
    }

    public Node(Guid processId, string nodeName, string elementId, NodeType nodeType) : this()
    {
        if (processId == Guid.Empty)
            throw new ArgumentException("Process ID cannot be empty", nameof(processId));
        
        if (string.IsNullOrWhiteSpace(nodeName))
            throw new ArgumentException("Node name cannot be null or empty", nameof(nodeName));
        
        if (string.IsNullOrWhiteSpace(elementId))
            throw new ArgumentException("Element ID cannot be null or empty", nameof(elementId));

        ProcessId = processId;
        NodeName = nodeName;
        ElementId = elementId;
        Type = nodeType;

        AddToHistory($"Node created: {nodeName} ({elementId})");
        AddDomainEvent(new NodeCreatedEvent(Id, ProcessId, NodeName, ElementId, Type, CreatedAt));
    }

    public void StartProcessing(Guid tokenId)
    {
        if (tokenId == Guid.Empty)
            throw new ArgumentException("Token ID cannot be empty", nameof(tokenId));

        if (State != NodeState.Pending && State != NodeState.Paused)
            throw new InvalidOperationException($"Cannot start processing node in {State} state. Node must be in Pending or Paused state.");

        State = NodeState.Processing;
        ProcessingStartedAt = DateTime.UtcNow;
        
        // Add token to current tokens
        if (!_currentTokenIds.Contains(tokenId))
        {
            _currentTokenIds.Add(tokenId);
        }
        
        
        
        AddToHistory($"Node started processing at {ProcessingStartedAt:yyyy-MM-dd HH:mm:ss} with token {tokenId}");
        AddDomainEvent(new NodeProcessingEvent(Id, ProcessId, tokenId, ProcessingStartedAt.Value));
    }

    public void Complete(Guid tokenId, Dictionary<string, object>? outputVariables = null)
    {
        if (tokenId == Guid.Empty)
            throw new ArgumentException("Token ID cannot be empty", nameof(tokenId));

        if (State != NodeState.Processing)
            throw new InvalidOperationException($"Cannot complete node in {State} state. Node must be in Processing state.");

        if (!_currentTokenIds.Contains(tokenId))
            throw new InvalidOperationException($"Token {tokenId} is not currently in this node.");

        State = NodeState.Completed;
        CompletedAt = DateTime.UtcNow;
        
        // Update variables if provided
        if (outputVariables != null)
        {
            foreach (var variable in outputVariables)
            {
                Variables[variable.Key] = variable.Value;
            }
        }

        // Move token from current to history
        _currentTokenIds.Remove(tokenId);
        
        // Find existing token history entry and mark it as completed
        var tokenHistoryEntry = _tokenHistory.FirstOrDefault(th => th.TokenId == tokenId && !th.CompletedAt.HasValue);
        if (tokenHistoryEntry != null)
        {
            tokenHistoryEntry.MarkCompleted(CompletedAt.Value, outputVariables);
        }
        else
        {
            // If no entry found, create a new one
            var newHistoryEntry = new NodeTokenHistoryEntry(
                Id,
                tokenId,
                ElementId,
                ProcessingStartedAt ?? CreatedAt,
                CompletedAt.Value,
                outputVariables);
            _tokenHistory.Add(newHistoryEntry);
        }
        
        AddToHistory($"Node completed at {CompletedAt:yyyy-MM-dd HH:mm:ss} with token {tokenId}");
        AddDomainEvent(new NodeCompletedEvent(Id, ProcessId, tokenId, CompletedAt.Value, outputVariables));
    }

    public void Fail(Guid tokenId, string errorCode = "UNKNOWN", string errorMessage = "An error occurred")
    {
        if (tokenId == Guid.Empty)
            throw new ArgumentException("Token ID cannot be empty", nameof(tokenId));

        if (string.IsNullOrWhiteSpace(errorCode))
            throw new ArgumentException("Error code cannot be null or empty", nameof(errorCode));
        
        if (string.IsNullOrWhiteSpace(errorMessage))
            throw new ArgumentException("Error message cannot be null or empty", nameof(errorMessage));

        if (State != NodeState.Processing)
            throw new InvalidOperationException($"Cannot fail node in {State} state. Node must be in Processing state.");

        State = NodeState.Failed;
        FailedAt = DateTime.UtcNow;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        
        // Remove token from current tokens
        _currentTokenIds.Remove(tokenId);
        
        
        
        AddToHistory($"Node failed at {FailedAt:yyyy-MM-dd HH:mm:ss}: {errorCode} - {errorMessage} (token: {tokenId})");
        AddDomainEvent(new NodeFailedEvent(Id, ProcessId, tokenId, FailedAt.Value, errorCode, errorMessage));
    }

    public void Pause(Guid tokenId, string? reason = null)
    {
        if (tokenId == Guid.Empty)
            throw new ArgumentException("Token ID cannot be empty", nameof(tokenId));

        if (State != NodeState.Processing)
            throw new InvalidOperationException($"Cannot pause node in {State} state. Node must be in Processing state.");

        if (!_currentTokenIds.Contains(tokenId))
            throw new InvalidOperationException($"Token {tokenId} is not currently in this node.");

        State = NodeState.Paused;
        
        
        var pauseReason = reason ?? "Paused by system";
        AddToHistory($"Node paused at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}: {pauseReason} (token: {tokenId})");
        AddDomainEvent(new NodePausedEvent(Id, ProcessId, tokenId, DateTime.UtcNow, reason));
    }

    public void Resume()
    {
        if (State != NodeState.Paused)
            throw new InvalidOperationException($"Cannot resume node in {State} state. Node must be in Paused state.");

        State = NodeState.Pending;
        
        AddToHistory($"Node resumed at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
    }

    public void Terminate()
    {
        if (State == NodeState.Completed)
            throw new InvalidOperationException("Cannot terminate a completed node.");

        State = NodeState.Terminated;
        
        AddToHistory($"Node terminated at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
    }

    public void SetVariable(string key, object value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Variable key cannot be null or empty", nameof(key));

        Variables[key] = value;
        
        AddToHistory($"Variable '{key}' set at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
    }

    public T? GetVariable<T>(string key)
    {
        if (Variables.TryGetValue(key, out var value) && value is T typedValue)
            return typedValue;
        
        return default;
    }

    /// <summary>
    /// Token reaches this node (enters the node)
    /// </summary>
    public void Reach(Guid tokenId, string elementId)
    {
        if (tokenId == Guid.Empty)
            throw new ArgumentException("Token ID cannot be empty", nameof(tokenId));

        if (string.IsNullOrWhiteSpace(elementId))
            throw new ArgumentException("Element ID cannot be null or empty", nameof(elementId));

        if (_currentTokenIds.Contains(tokenId))
            throw new InvalidOperationException($"Token {tokenId} is already in this node.");

        _currentTokenIds.Add(tokenId);
        var tokenHistoryEntry = new NodeTokenHistoryEntry(
            Id,
            tokenId,
            elementId,
            DateTime.UtcNow);
        _tokenHistory.Add(tokenHistoryEntry);
        
        AddToHistory($"Token {tokenId} reached node at element {elementId} at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
    }

    /// <summary>
    /// Adds a token to the node (legacy method, use Reach instead)
    /// </summary>
    public void AddTokenToNode(Guid tokenId, string? elementId = null)
    {
        Reach(tokenId, elementId ?? ElementId);
    }

    /// <summary>
    /// Token leaves this node
    /// </summary>
    public void Leave(Guid tokenId)
    {
        if (tokenId == Guid.Empty)
            throw new ArgumentException("Token ID cannot be empty", nameof(tokenId));

        if (!_currentTokenIds.Contains(tokenId))
            throw new InvalidOperationException($"Token {tokenId} is not currently in this node.");

        _currentTokenIds.Remove(tokenId);
        
        AddToHistory($"Token {tokenId} left node at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
    }

    /// <summary>
    /// Removes a token from the node (legacy method, use Leave instead)
    /// </summary>
    public void RemoveTokenFromNode(Guid tokenId)
    {
        Leave(tokenId);
    }

    /// <summary>
    /// Checks if a token is currently in this node
    /// </summary>
    public bool HasToken(Guid tokenId)
    {
        return _currentTokenIds.Contains(tokenId);
    }

    /// <summary>
    /// Gets the count of tokens currently in this node
    /// </summary>
    public int TokenCount => _currentTokenIds.Count;

    /// <summary>
    /// Checks if node is ready to proceed (all required tokens have arrived)
    /// For Join gateways, this checks if all incoming tokens have arrived
    /// </summary>
    public bool IsReadyToProceed(int requiredTokenCount = 1)
    {
        return _currentTokenIds.Count >= requiredTokenCount;
    }

    /// <summary>
    /// Gets all tokens currently in this node
    /// </summary>
    public IReadOnlyCollection<Guid> GetCurrentTokens()
    {
        return _currentTokenIds.AsReadOnly();
    }

    /// <summary>
    /// Checks if a specific token is in this node
    /// </summary>
    public bool ContainsToken(Guid tokenId)
    {
        return _currentTokenIds.Contains(tokenId);
    }

    /// <summary>
    /// Gets the count of tokens that have passed through this node
    /// </summary>
    public int TotalTokensPassed => _tokenHistory.Count;

    /// <summary>
    /// Adds a parent node (node that comes before this node)
    /// </summary>
    public void AddParentNode(Guid parentNodeId)
    {
        if (parentNodeId == Guid.Empty)
            throw new ArgumentException("Parent node ID cannot be empty", nameof(parentNodeId));

        if (parentNodeId == Id)
            throw new InvalidOperationException("Node cannot be its own parent.");

        if (!_parentNodeIds.Contains(parentNodeId))
        {
            _parentNodeIds.Add(parentNodeId);
            AddToHistory($"Parent node {parentNodeId} added");
        }
    }

    /// <summary>
    /// Adds multiple parent nodes (for Join/Merge scenarios)
    /// </summary>
    public void AddParentNodes(IEnumerable<Guid> parentNodeIds)
    {
        if (parentNodeIds == null)
            throw new ArgumentNullException(nameof(parentNodeIds));

        foreach (var parentNodeId in parentNodeIds)
        {
            AddParentNode(parentNodeId);
        }
    }

    /// <summary>
    /// Removes a parent node
    /// </summary>
    public void RemoveParentNode(Guid parentNodeId)
    {
        if (_parentNodeIds.Remove(parentNodeId))
        {
            AddToHistory($"Parent node {parentNodeId} removed");
        }
    }

    /// <summary>
    /// Adds a child node (node that comes after this node)
    /// </summary>
    public void AddChildNode(Guid childNodeId)
    {
        if (childNodeId == Guid.Empty)
            throw new ArgumentException("Child node ID cannot be empty", nameof(childNodeId));

        if (childNodeId == Id)
            throw new InvalidOperationException("Node cannot be its own child.");

        if (!_childNodeIds.Contains(childNodeId))
        {
            _childNodeIds.Add(childNodeId);
            AddToHistory($"Child node {childNodeId} added");
        }
    }

    /// <summary>
    /// Adds multiple child nodes (for Fork scenarios)
    /// </summary>
    public void AddChildNodes(IEnumerable<Guid> childNodeIds)
    {
        if (childNodeIds == null)
            throw new ArgumentNullException(nameof(childNodeIds));

        foreach (var childNodeId in childNodeIds)
        {
            AddChildNode(childNodeId);
        }
    }

    /// <summary>
    /// Removes a child node
    /// </summary>
    public void RemoveChildNode(Guid childNodeId)
    {
        if (_childNodeIds.Remove(childNodeId))
        {
            AddToHistory($"Child node {childNodeId} removed");
        }
    }

    /// <summary>
    /// Checks if this node has a specific parent node
    /// </summary>
    public bool HasParentNode(Guid parentNodeId)
    {
        return _parentNodeIds.Contains(parentNodeId);
    }

    /// <summary>
    /// Checks if this node has a specific child node
    /// </summary>
    public bool HasChildNode(Guid childNodeId)
    {
        return _childNodeIds.Contains(childNodeId);
    }

    /// <summary>
    /// Gets the count of parent nodes (incoming nodes)
    /// </summary>
    public int ParentNodeCount => _parentNodeIds.Count;

    /// <summary>
    /// Gets the count of child nodes (outgoing nodes)
    /// </summary>
    public int ChildNodeCount => _childNodeIds.Count;

    /// <summary>
    /// Checks if this node is a Fork node (has multiple child nodes)
    /// </summary>
    public bool IsForkNode => _childNodeIds.Count > 1;

    /// <summary>
    /// Checks if this node is a Join node (has multiple parent nodes)
    /// </summary>
    public bool IsJoinNode => _parentNodeIds.Count > 1;

    /// <summary>
    /// Checks if this node is ready for Join (all parent nodes have completed)
    /// This would need to check parent node states through repository
    /// </summary>
    public bool IsReadyForJoin(int requiredParentCount)
    {
        return _parentNodeIds.Count >= requiredParentCount;
    }

    /// <summary>
    /// Gets all paths that lead to this node (from start events)
    /// Returns list of parent node IDs in order
    /// </summary>
    public IReadOnlyCollection<Guid> GetIncomingPaths()
    {
        return _parentNodeIds.AsReadOnly();
    }

    /// <summary>
    /// Gets all paths that lead from this node (to end events)
    /// Returns list of child node IDs in order
    /// </summary>
    public IReadOnlyCollection<Guid> GetOutgoingPaths()
    {
        return _childNodeIds.AsReadOnly();
    }

    /// <summary>
    /// Checks if this node can receive tokens from all parent nodes (for Join)
    /// </summary>
    public bool CanReceiveFromAllParents(int expectedParentCount)
    {
        return _parentNodeIds.Count == expectedParentCount;
    }

    /// <summary>
    /// Checks if this node can send tokens to all child nodes (for Fork)
    /// </summary>
    public bool CanSendToAllChildren(int expectedChildCount)
    {
        return _childNodeIds.Count == expectedChildCount;
    }

    private void AddToHistory(string entry)
    {
        _history.Add($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] {entry}");
    }
}

