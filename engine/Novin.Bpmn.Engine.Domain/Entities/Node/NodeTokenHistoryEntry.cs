using Novin.Bpmn.Engine.Domain.Common;

namespace Novin.Bpmn.Engine.Domain.Entities;

/// <summary>
/// Child Entity representing a token history entry in a node
/// </summary>
public class NodeTokenHistoryEntry : BaseEntity
{
    public Guid NodeId { get; private set; }
    public Guid TokenId { get; private set; }
    public string ElementId { get; private set; }
    public DateTime ReachedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public Dictionary<string, object>? OutputVariables { get; private set; }

    private NodeTokenHistoryEntry() : base()
    {
    }

    public NodeTokenHistoryEntry(
        Guid nodeId,
        Guid tokenId,
        string elementId,
        DateTime reachedAt,
        DateTime? completedAt = null,
        Dictionary<string, object>? outputVariables = null) : base()
    {
        if (nodeId == Guid.Empty)
            throw new ArgumentException("Node ID cannot be empty", nameof(nodeId));

        if (tokenId == Guid.Empty)
            throw new ArgumentException("Token ID cannot be empty", nameof(tokenId));

        if (string.IsNullOrWhiteSpace(elementId))
            throw new ArgumentException("Element ID cannot be null or empty", nameof(elementId));

        NodeId = nodeId;
        TokenId = tokenId;
        ElementId = elementId;
        ReachedAt = reachedAt;
        CompletedAt = completedAt;
        OutputVariables = outputVariables ?? new Dictionary<string, object>();
    }

    public void MarkCompleted(DateTime completedAt, Dictionary<string, object>? outputVariables = null)
    {
        if (CompletedAt.HasValue)
            throw new InvalidOperationException("Token history entry is already completed.");

        CompletedAt = completedAt;
        if (outputVariables != null)
        {
            OutputVariables = outputVariables;
        }
    }
}

