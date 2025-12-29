using MediatR;
using Novin.Bpmn.Engine.Application.Commands.NodeDispatch;
using Novin.Bpmn.Engine.Domain.Entities;

public sealed class NodeCompletedDomainEventHandler
    : INotificationHandler<NodeCompletedDomainEvent>
{
    private readonly IMediator _mediator;
    private readonly ILogger<NodeCompletedDomainEventHandler> _logger;

    public NodeCompletedDomainEventHandler(
        IMediator mediator,
        ILogger<NodeCompletedDomainEventHandler> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Handle(NodeCompletedDomainEvent e, CancellationToken ct)
    {
        _logger.LogInformation(
            "[NODE-COMPLETE] NodeId={NodeId} TokenId={TokenId} ElementId={ElementId}",
            e.NodeId, e.TokenId, e.ElementId);

        // 👉 trigger NAVIGATION phase
        await _mediator.Send(
            new DispatchNodeNavigateCommand(e.NodeId),
            ct);
    }
}