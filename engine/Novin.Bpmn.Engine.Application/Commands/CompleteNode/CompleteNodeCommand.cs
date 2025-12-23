using MediatR;

namespace Novin.Bpmn.Engine.Application.Commands.CompleteNode;

public class CompleteNodeCommand : IRequest<CompleteNodeResult>
{
    public Guid NodeId { get; set; }
    public Guid? TokenId { get; set; }
    public Dictionary<string, object>? OutputVariables { get; set; }

    public CompleteNodeCommand(Guid nodeId, Guid? tokenId = null, Dictionary<string, object>? outputVariables = null)
    {
        NodeId = nodeId;
        TokenId = tokenId;
        OutputVariables = outputVariables;
    }
}

public class CompleteNodeResult
{
    public Guid NodeId { get; set; }
    public DateTime CompletedAt { get; set; }
    public bool Success { get; set; }
}

