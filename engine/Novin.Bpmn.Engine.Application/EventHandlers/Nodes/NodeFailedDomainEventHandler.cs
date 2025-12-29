using MediatR;
using Novin.Bpmn.Engine.Domain.Entities;

public sealed class NodeFailedDomainEventHandler
    : INotificationHandler<NodeFailedDomainEvent>
{
    private readonly IMediator _mediator;
    private readonly ILogger<NodeFailedDomainEventHandler> _logger;

    public NodeFailedDomainEventHandler(
        IMediator mediator,
        ILogger<NodeFailedDomainEventHandler> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Handle(NodeFailedDomainEvent e, CancellationToken ct)
    {
        _logger.LogError(
            "[NODE-FAILED] NodeId={NodeId} TokenId={TokenId} Error={Error}",
            e.NodeId, e.TokenId, e.ErrorMessage);
        //
        // // 1️⃣ open incident
        // await _mediator.Send(
        //     new OpenIncidentCommand(
        //         processId: e.ProcessId,
        //         tokenId: e.TokenId,
        //         nodeInstanceId: e.NodeId,
        //         elementId: e.ElementId,
        //         message: e.ErrorMessage),
        //     ct);
        //
        // // 2️⃣ optional: trigger boundary error evaluation
        // await _mediator.Send(
        //     new EvaluateBoundaryErrorCommand(
        //         e.ProcessId,
        //         e.TokenId,
        //         e.NodeId,
        //         e.ElementId),
        //     ct);
    }
}