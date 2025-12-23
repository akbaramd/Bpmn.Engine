using Novin.Bpmn.Engine.Domain.Common;
using Novin.Bpmn.Engine.Domain.Events;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Domain.Entities;

/// <summary>
/// Aggregate root representing a token in BPMN execution flow
/// </summary>
public class Token : BaseAggregateRoot
{
    public Guid ProcessId { get; private set; }
    public string CurrentElementId { get; private set; }
    public Guid? CurrentNodeId { get; private set; }
    public TokenState State { get; private set; }
    public Guid? ParentTokenId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ActivatedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    
    // History of nodes this token has passed through (Child Entities)
    private readonly List<TokenHistoryEntry> _tokenHistory = new();
    public IReadOnlyCollection<TokenHistoryEntry> TokenHistory => _tokenHistory.AsReadOnly();
    
    // Parent node IDs (for Fork/Join scenarios)
    private readonly List<Guid> _parentNodeIds = new();
    public IReadOnlyCollection<Guid> ParentNodeIds => _parentNodeIds.AsReadOnly();
    
    // Next nodes that token will visit (element IDs)
    private readonly List<string> _nextNodes = new();
    public IReadOnlyCollection<string> NextNodes => _nextNodes.AsReadOnly();
    
    private readonly List<string> _history = new();
    public IReadOnlyCollection<string> History => _history.AsReadOnly();

    private Token() : base()
    {
        State = TokenState.Created;
        CreatedAt = DateTime.UtcNow;
    }

    public Token(Guid processId, string initialElementId, Guid initialNodeId, Guid? parentTokenId = null) : this()
    {
        if (processId == Guid.Empty)
            throw new ArgumentException("Process ID cannot be empty", nameof(processId));
        
        if (string.IsNullOrWhiteSpace(initialElementId))
            throw new ArgumentException("Initial element ID cannot be null or empty", nameof(initialElementId));

        if (initialNodeId == Guid.Empty)
            throw new ArgumentException("Initial node ID cannot be empty", nameof(initialNodeId));

        ProcessId = processId;
        CurrentElementId = initialElementId;
        CurrentNodeId = initialNodeId;
        ParentTokenId = parentTokenId;
        
        AddToHistory($"Token created at element: {initialElementId} in node: {initialNodeId}");
        AddDomainEvent(new TokenCreatedEvent(Id, ProcessId, initialElementId, parentTokenId, CreatedAt));
    }

    public void Activate()
    {
        if (State != TokenState.Created)
            throw new InvalidOperationException($"Cannot activate token in {State} state.");

        State = TokenState.Active;
        ActivatedAt = DateTime.UtcNow;
        
    }

    /// <summary>
    /// Moves token to the next step (element and node)
    /// </summary>
    public void MoveToNextStep(string nextElementId, Guid nextNodeId)
    {
        if (string.IsNullOrWhiteSpace(nextElementId))
            throw new ArgumentException("Next element ID cannot be null or empty", nameof(nextElementId));

        if (nextNodeId == Guid.Empty)
            throw new ArgumentException("Next node ID cannot be empty", nameof(nextNodeId));

        if (State != TokenState.Active)
            throw new InvalidOperationException($"Cannot move token in {State} state. Token must be Active.");

        if (!CurrentNodeId.HasValue)
            throw new InvalidOperationException("Token must be in a node before moving to next step.");

        var fromElementId = CurrentElementId;
        var fromNodeId = CurrentNodeId.Value;
        
        // Add previous node as parent
        if (!_parentNodeIds.Contains(fromNodeId))
        {
            _parentNodeIds.Add(fromNodeId);
        }

        // Update current position
        CurrentElementId = nextElementId;
        CurrentNodeId = nextNodeId;
        
        // Remove from next nodes if it was planned
        _nextNodes.Remove(nextElementId);
        
        AddToHistory($"Token moved from {fromElementId} (Node: {fromNodeId}) to {nextElementId} (Node: {nextNodeId})");
        AddDomainEvent(new TokenMovedEvent(Id, ProcessId, fromElementId, nextElementId, DateTime.UtcNow));
    }

    public void Wait()
    {
        if (State != TokenState.Active)
            throw new InvalidOperationException($"Cannot set token to waiting in {State} state.");

        State = TokenState.Waiting;
        
    }

    public void Resume()
    {
        if (State != TokenState.Waiting)
            throw new InvalidOperationException($"Cannot resume token in {State} state. Token must be Waiting.");

        State = TokenState.Active;
        
    }

    public void Complete()
    {
        if (State != TokenState.Active)
            throw new InvalidOperationException($"Cannot complete token in {State} state. Token must be Active.");

        State = TokenState.Completed;
        CompletedAt = DateTime.UtcNow;
        
    }

    public void Terminate()
    {
        if (State == TokenState.Completed)
            throw new InvalidOperationException("Cannot terminate a completed token.");

        State = TokenState.Terminated;
        
    }

    /// <summary>
    /// Token reaches a node (enters the node)
    /// </summary>
    public void Reach(Guid nodeId, string elementId, Guid? parentNodeId = null)
    {
        if (nodeId == Guid.Empty)
            throw new ArgumentException("Node ID cannot be empty", nameof(nodeId));

        if (string.IsNullOrWhiteSpace(elementId))
            throw new ArgumentException("Element ID cannot be null or empty", nameof(elementId));

        if (State != TokenState.Active && State != TokenState.Created)
            throw new InvalidOperationException($"Cannot reach node in {State} state. Token must be Active or Created.");

        var previousNodeId = CurrentNodeId;
        var previousElementId = CurrentElementId;

        // Add previous node as parent if exists
        if (previousNodeId.HasValue && !_parentNodeIds.Contains(previousNodeId.Value))
        {
            _parentNodeIds.Add(previousNodeId.Value);
        }

        // Add parent node if provided (for Fork scenarios)
        if (parentNodeId.HasValue && parentNodeId.Value != Guid.Empty && !_parentNodeIds.Contains(parentNodeId.Value))
        {
            _parentNodeIds.Add(parentNodeId.Value);
        }

        CurrentNodeId = nodeId;
        CurrentElementId = elementId;

        // Remove from next nodes if it was planned
        _nextNodes.Remove(elementId);
        
        AddToHistory($"Token reached node {nodeId} at element {elementId} (from: {previousElementId ?? "start"})");
        
        // If token was just created, activate it
        if (State == TokenState.Created)
        {
            Activate();
        }
    }

    /// <summary>
    /// Token enters a node (legacy method, use Reach instead)
    /// </summary>
    public void EnterNode(Guid nodeId, string elementId)
    {
        Reach(nodeId, elementId);
    }

    /// <summary>
    /// Token leaves the current node
    /// </summary>
    public void Leave()
    {
        if (!CurrentNodeId.HasValue)
            throw new InvalidOperationException("Token is not currently in any node.");

        var nodeId = CurrentNodeId.Value;
        var elementId = CurrentElementId;
        
        CurrentNodeId = null;
        
        AddToHistory($"Token left node {nodeId} at element {elementId}");
    }

    /// <summary>
    /// Token leaves node (legacy method, use Leave instead)
    /// </summary>
    public void LeaveNode()
    {
        Leave();
    }

    /// <summary>
    /// Checks if token is currently in a node
    /// </summary>
    public bool IsInNode()
    {
        return CurrentNodeId.HasValue;
    }

    /// <summary>
    /// Checks if token is at a specific node
    /// </summary>
    public bool IsAtNode(Guid nodeId)
    {
        return CurrentNodeId == nodeId;
    }

    /// <summary>
    /// Checks if token is at a specific element
    /// </summary>
    public bool IsAtElement(string elementId)
    {
        return CurrentElementId == elementId;
    }

    /// <summary>
    /// Adds next nodes that token will visit (for Gateway/Fork scenarios)
    /// </summary>
    public void AddNextNodes(IEnumerable<string> nextElementIds)
    {
        if (nextElementIds == null)
            throw new ArgumentNullException(nameof(nextElementIds));

        foreach (var elementId in nextElementIds)
        {
            if (!string.IsNullOrWhiteSpace(elementId) && !_nextNodes.Contains(elementId))
            {
                _nextNodes.Add(elementId);
            }
        }

        AddToHistory($"Token planned to visit next nodes: {string.Join(", ", nextElementIds)}");
    }

    /// <summary>
    /// Adds a single next node
    /// </summary>
    public void AddNextNode(string nextElementId)
    {
        if (string.IsNullOrWhiteSpace(nextElementId))
            throw new ArgumentException("Next element ID cannot be null or empty", nameof(nextElementId));

        if (!_nextNodes.Contains(nextElementId))
        {
            _nextNodes.Add(nextElementId);
            AddToHistory($"Token planned to visit next node: {nextElementId}");
        }
    }

    /// <summary>
    /// Clears all next nodes
    /// </summary>
    public void ClearNextNodes()
    {
        _nextNodes.Clear();
        AddToHistory("Token next nodes cleared");
    }

    /// <summary>
    /// Adds a history entry when token reaches a node
    /// Internal method - only callable from within the Domain layer (aggregate roots)
    /// </summary>
    internal void AddHistoryEntry(Guid nodeId, string elementId, string nodeName, TokenState state, Dictionary<string, object>? variables = null)
    {
        var historyEntry = new TokenHistoryEntry(
            Id,
            nodeId,
            elementId,
            nodeName,
            DateTime.UtcNow,
            state,
            variables);
        
        _tokenHistory.Add(historyEntry);
    }

    /// <summary>
    /// Marks the last history entry as left
    /// </summary>
    public void MarkLastHistoryEntryLeft()
    {
        var lastEntry = _tokenHistory.LastOrDefault();
        if (lastEntry != null && !lastEntry.LeftAt.HasValue)
        {
            lastEntry.MarkLeft(DateTime.UtcNow);
        }
    }

    /// <summary>
    /// Checks if token has visited a specific node
    /// </summary>
    public bool HasVisitedNode(Guid nodeId)
    {
        return _tokenHistory.Any(th => th.NodeId == nodeId);
    }

    /// <summary>
    /// Checks if token has visited a specific element
    /// </summary>
    public bool HasVisitedElement(string elementId)
    {
        return _tokenHistory.Any(th => th.ElementId == elementId);
    }

    /// <summary>
    /// Gets the last visited node
    /// </summary>
    public TokenHistoryEntry? GetLastHistoryEntry()
    {
        return _tokenHistory.LastOrDefault();
    }

    /// <summary>
    /// Gets history entries for a specific node
    /// </summary>
    public IEnumerable<TokenHistoryEntry> GetHistoryEntriesForNode(Guid nodeId)
    {
        return _tokenHistory.Where(th => th.NodeId == nodeId);
    }

    /// <summary>
    /// Fork token - creates child tokens for parallel execution
    /// Returns the next element IDs (not node IDs) so handler can create child tokens
    /// </summary>
    public List<string> Fork(IEnumerable<string> nextElementIds)
    {
        if (nextElementIds == null || !nextElementIds.Any())
            throw new ArgumentException("Next element IDs cannot be null or empty", nameof(nextElementIds));

        // Add all next nodes
        AddNextNodes(nextElementIds);

        AddToHistory($"Token forked to {nextElementIds.Count()} paths: {string.Join(", ", nextElementIds)}");

        // Return the next element IDs so handler can create child tokens
        return nextElementIds.ToList();
    }

    private void AddToHistory(string entry)
    {
        _history.Add($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] {entry}");
    }
}

