using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Commands.StartProcess;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Events;

namespace Novin.Bpmn.Engine.Application.EventHandlers.Process;

public sealed class ProcessInstanceCreatedEventHandler : INotificationHandler<ProcessInstanceCreatedEvent>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProcessStartPolicy _startPolicy;
    private readonly IMediator _mediator;
    private readonly ILogger<ProcessInstanceCreatedEventHandler> _logger;

    public ProcessInstanceCreatedEventHandler(
        IUnitOfWork unitOfWork,
        IProcessStartPolicy startPolicy,
        IMediator mediator,
        ILogger<ProcessInstanceCreatedEventHandler> logger)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _startPolicy = startPolicy ?? throw new ArgumentNullException(nameof(startPolicy));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async System.Threading.Tasks.Task Handle(ProcessInstanceCreatedEvent notification, CancellationToken cancellationToken)
    {
        var process = await _unitOfWork.Processes.GetByIdAsync(notification.ProcessId, cancellationToken);
        if (process is null)
        {
            _logger.LogWarning("Process {ProcessId} not found while handling ProcessInstanceCreatedEvent", notification.ProcessId);
            return;
        }

        if (!_startPolicy.ShouldAutoStart(process))
        {
            _logger.LogInformation("Auto-start disabled for process {ProcessId}. Manual start required.", notification.ProcessId);
            return;
        }

        _logger.LogInformation("Auto-start policy triggered for process {ProcessId}", notification.ProcessId);
        await _mediator.Send(new StartProcessCommand(notification.ProcessId), cancellationToken);
    }
}

