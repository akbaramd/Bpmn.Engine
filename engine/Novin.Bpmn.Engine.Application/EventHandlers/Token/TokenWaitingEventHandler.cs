using MediatR;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.Events;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

public sealed class TokenWaitingEventHandler
    : INotificationHandler<TokenWaitingEvent>
{
    private readonly IClientCommunicationService _clientCommunication;
    private readonly IWorkerRepository _workerRepository;
    private readonly ILogger<TokenWaitingEventHandler> _logger;

    public TokenWaitingEventHandler(
        IClientCommunicationService clientCommunication,
        IWorkerRepository workerRepository,
        ILogger<TokenWaitingEventHandler> logger)
    {
        _clientCommunication = clientCommunication ?? throw new ArgumentNullException(nameof(clientCommunication));
        _workerRepository = workerRepository ?? throw new ArgumentNullException(nameof(workerRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async System.Threading.Tasks.Task Handle(TokenWaitingEvent notification, CancellationToken ct)
    {
        try
        {
            // Only handle service task workers
            if (!notification.WorkerId.HasValue)
            {
                return;
            }

            var worker = await _workerRepository.GetByIdAsync(notification.WorkerId.Value, ct);
            if (worker == null)
            {
                _logger.LogWarning("Worker {WorkerId} not found for token waiting event", notification.WorkerId.Value);
                return;
            }

            // Only route service task workers
            if (worker.Type is not (WorkerType.ServiceTask or WorkerType.UserTask))
            {
                return;
            }

            _logger.LogInformation("Routing service task worker {WorkerId} to clients for token {TokenId}",
                worker.Id, notification.TokenId);

            // Route to client(s) - this will mark worker as started
            await _clientCommunication.RouteServiceTaskToClientsAsync(worker, ct);

            _logger.LogInformation("Service task worker {WorkerId} routed to clients successfully",
                worker.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error routing service task worker {WorkerId} to clients",
                notification.WorkerId);
            throw;
        }
    }
}