using MediatR;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.Events;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

public sealed class WorkerCompletedEventHandler
    : INotificationHandler<WorkerCompletedDomainEvent>
{
    private readonly IMediator _mediator;
    private readonly ILogger<WorkerCompletedEventHandler> _logger;

    public WorkerCompletedEventHandler(
        IMediator mediator,
        ILogger<WorkerCompletedEventHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async System.Threading.Tasks.Task Handle(WorkerCompletedDomainEvent notification, CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Handling worker {WorkerId} completion event", notification.WorkerId);

            // Send command to resume token after worker completion
            var command = new Commands.ResumeTokenAfterWorkerCompletionCommand(
                WorkerId: notification.WorkerId,
                Result: notification.Result,
                CompletedBy: notification.CompletedBy);

            await _mediator.Send(command, ct);

            _logger.LogInformation("Command sent to resume token after worker {WorkerId} completion", notification.WorkerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling worker {WorkerId} completion event", notification.WorkerId);
            throw;
        }
    }
}