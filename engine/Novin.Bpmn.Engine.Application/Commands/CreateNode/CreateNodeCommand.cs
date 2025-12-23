using MediatR;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.Commands.CreateNode;

public class CreateNodeCommand : IRequest<CreateNodeResult>
{
    public Guid ProcessId { get; set; }
    public string NodeName { get; set; } = string.Empty;
    public string ElementId { get; set; } = string.Empty;
    public NodeType NodeType { get; set; }

    public CreateNodeCommand(Guid processId, string nodeName, string elementId, NodeType nodeType)
    {
        ProcessId = processId;
        NodeName = nodeName;
        ElementId = elementId;
        NodeType = nodeType;
    }
}

public class CreateNodeResult
{
    public Guid NodeId { get; set; }
    public string NodeName { get; set; } = string.Empty;
    public string ElementId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

