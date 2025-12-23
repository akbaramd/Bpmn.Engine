using MediatR;

namespace Novin.Bpmn.Engine.Application.Commands.ProcessNode;

public class ProcessNodeCommand : IRequest<ProcessNodeResult>
{
    public Guid NodeId { get; set; }
    public Guid? TokenId { get; set; }

    public ProcessNodeCommand(Guid nodeId, Guid? tokenId = null)
    {
        NodeId = nodeId;
        TokenId = tokenId;
    }
}

public class ProcessNodeResult
{
    public Guid NodeId { get; set; }
    public DateTime StartedAt { get; set; }
    public bool Success { get; set; }
}

