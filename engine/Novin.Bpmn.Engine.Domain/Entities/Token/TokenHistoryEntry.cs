using Novin.Bpmn.Engine.Domain.Common;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Domain.Entities;

/// <summary>
/// Child Entity representing a history entry for a token's movement through nodes
/// </summary>
public class TokenHistoryEntry : BaseEntity
{
    public Guid TokenId { get; private set; }
    public Guid NodeId { get; private set; }
    public string ElementId { get; private set; }
    public string NodeName { get; private set; }
    public DateTime ReachedAt { get; private set; }
    public DateTime? LeftAt { get; private set; }
    public TokenState State { get; private set; }
    public Dictionary<string, object>? Variables { get; private set; }

    private TokenHistoryEntry() : base()
    {
    }

    public TokenHistoryEntry(
        Guid tokenId,
        Guid nodeId,
        string elementId,
        string nodeName,
        DateTime reachedAt,
        TokenState state,
        Dictionary<string, object>? variables = null) : base()
    {
        if (tokenId == Guid.Empty)
            throw new ArgumentException("Token ID cannot be empty", nameof(tokenId));

        if (nodeId == Guid.Empty)
            throw new ArgumentException("Node ID cannot be empty", nameof(nodeId));

        if (string.IsNullOrWhiteSpace(elementId))
            throw new ArgumentException("Element ID cannot be null or empty", nameof(elementId));

        if (string.IsNullOrWhiteSpace(nodeName))
            throw new ArgumentException("Node name cannot be null or empty", nameof(nodeName));

        TokenId = tokenId;
        NodeId = nodeId;
        ElementId = elementId;
        NodeName = nodeName;
        ReachedAt = reachedAt;
        State = state;
        Variables = variables ?? new Dictionary<string, object>();
    }

    public void MarkLeft(DateTime leftAt)
    {
        if (LeftAt.HasValue)
            throw new InvalidOperationException("Token has already left this node.");

        LeftAt = leftAt;
    }

    public void UpdateVariables(Dictionary<string, object> variables)
    {
        if (variables == null)
            throw new ArgumentNullException(nameof(variables));

        Variables = variables;
    }
}

