using MediatR;
using Microsoft.AspNetCore.SignalR;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Features.Workers.Commands;
using Novin.Bpmn.Engine.Application.Hubs;
using Novin.Bpmn.Engine.Domain.Communication;
using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Application.Services;

/// <summary>
/// SignalR-based implementation for client communication
/// </summary>
public class SignalRClientCommunicationService : IClientCommunicationService
{
    private readonly IHubContext<ClientHub> _hubContext;
    private readonly IClientRegistry _clientRegistry;
    private readonly IWorkerRepository _workerRepository;
    private readonly IProcessRepository _processRepository;
    private readonly ITokenRepository _tokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IVariableMappingService _variableMapping;
    private readonly IBpmnRuntimeContextFactory _ctxFactory;
    private readonly IMediator _mediator;
    private readonly ILogger<SignalRClientCommunicationService> _logger;

    public SignalRClientCommunicationService(
        IHubContext<ClientHub> hubContext,
        IClientRegistry clientRegistry,
        IWorkerRepository workerRepository,
        IProcessRepository processRepository,
        ITokenRepository tokenRepository,
        IUnitOfWork unitOfWork,
        IVariableMappingService variableMapping,
        IBpmnRuntimeContextFactory ctxFactory,
        IMediator mediator,
        ILogger<SignalRClientCommunicationService> logger)
    {
        _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        _clientRegistry = clientRegistry ?? throw new ArgumentNullException(nameof(clientRegistry));
        _workerRepository = workerRepository ?? throw new ArgumentNullException(nameof(workerRepository));
        _processRepository = processRepository ?? throw new ArgumentNullException(nameof(processRepository));
        _tokenRepository = tokenRepository ?? throw new ArgumentNullException(nameof(tokenRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _variableMapping = variableMapping ?? throw new ArgumentNullException(nameof(variableMapping));
        _ctxFactory = ctxFactory ?? throw new ArgumentNullException(nameof(ctxFactory));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task RouteServiceTaskToClientsAsync(Worker worker, CancellationToken cancellationToken = default)
    {
        if (worker == null)
            throw new ArgumentNullException(nameof(worker));

        if (worker.Type is not ( WorkerType.ServiceTask or WorkerType.UserTask))
            throw new ArgumentException("Worker must be of type ServiceTask", nameof(worker));

        _logger.LogInformation("Routing worker {WorkerId} to client {ClientId}",
            worker.Id, worker.Metadata.GetValueOrDefault("targetClientId"));

        var targetClientId = worker.Metadata.GetValueOrDefault("targetClientId")?.ToString();

        if (string.IsNullOrEmpty(targetClientId))
        {
            _logger.LogWarning("No target client ID specified for worker {WorkerId}", worker.Id);
            return;
        }

        try
        {
            // Find the client by ID to get the connection ID
            var client = await _clientRegistry.GetClientByIdAsync(targetClientId);
            if (client == null)
            {
                _logger.LogWarning("Client {ClientId} not found for worker {WorkerId}",
                    targetClientId, worker.Id);
                return;
            }

            if (string.IsNullOrEmpty(client.ConnectionId))
            {
                _logger.LogWarning("Client {ClientId} has no connection ID for worker {WorkerId}",
                    targetClientId, worker.Id);
                return;
            }

            // Mark worker as started
            worker.Claim(client.ClientId);
            worker.MarkStarted(client.ClientId);
            await _workerRepository.UpdateAsync(worker, cancellationToken);

            // Create the service task request payload
            var request = new WorkerTaskRequest
            {
                WorkerId = worker.Id,
                ExecutionId = worker.Id, // For backward compatibility
                ProcessId = worker.ProcessId,
                TokenId = worker.TokenId,
                ElementId = worker.ElementId,
                TaskName = worker.TaskName,
                Implementation = worker.Metadata.GetValueOrDefault("implementation") ?? "",
                Metadata = worker.Metadata,
                Variables = worker.Variables
            };

            // Send to the specific client via SignalR using the connection ID
            await _hubContext.Clients
                .Client(client.ConnectionId)
                .SendAsync("ExecuteServiceTask", request, cancellationToken);

            _logger.LogInformation("Worker {WorkerId} sent to client {ClientId} (Connection: {ConnectionId})",
                worker.Id, targetClientId, client.ConnectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to route worker {WorkerId} to client {ClientId}",
                worker.Id, targetClientId);
            throw;
        }
    }

    public async Task NotifyWorkerCompletedAsync(Guid workerId, Dictionary<string, string>? result = null, string? completedBy = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Worker {WorkerId} completed notification", workerId);

        // Send command to complete the worker
        var command = new CompleteWorkerCommand(
            WorkerId: workerId,
            Result: result,
            CompletedBy: completedBy);

        await _mediator.Send(command, cancellationToken);

        _logger.LogInformation("Command sent to complete worker {WorkerId}", workerId);
    }

    public async Task NotifyWorkerFailedAsync(Guid workerId, string error, string? completedBy = null, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Worker {WorkerId} failed: {Error}", workerId, error);

        // Send command to fail the worker
        var command = new FailWorkerCommand(
            WorkerId: workerId,
            Error: error,
            CompletedBy: completedBy);

        await _mediator.Send(command, cancellationToken);

        _logger.LogInformation("Command sent to fail worker {WorkerId}", workerId);
    }
}