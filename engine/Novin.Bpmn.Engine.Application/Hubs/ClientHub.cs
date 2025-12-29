using Microsoft.AspNetCore.SignalR;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.Hubs;

/// <summary>
/// SignalR hub for BPMN engine client communications
/// </summary>
public class ClientHub : Hub
{
    private readonly ILogger<ClientHub> _logger;
    private readonly IClientRegistry _clientRegistry;
    private readonly IClientCommunicationService _clientCommunication;

    public ClientHub(
        ILogger<ClientHub> logger,
        IClientRegistry clientRegistry,
        IClientCommunicationService clientCommunication)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _clientRegistry = clientRegistry ?? throw new ArgumentNullException(nameof(clientRegistry));
        _clientCommunication = clientCommunication ?? throw new ArgumentNullException(nameof(clientCommunication));
    }

    /// <summary>
    /// Called when a client connects
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Called when a client disconnects
    /// </summary>
    /// <param name="exception">The exception that caused the disconnection</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}. Exception: {Exception}",
            Context.ConnectionId, exception?.Message);

        // Unregister the client
        await _clientRegistry.UnregisterClientAsync(Context.ConnectionId);

        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Registers a client with the BPMN engine
    /// </summary>
    /// <param name="clientInfo">Client registration information</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task RegisterClient(ClientInfo clientInfo)
    {
        try
        {
            _logger.LogInformation("Registering client: {ClientId} from connection {ConnectionId}",
                clientInfo.ClientId, Context.ConnectionId);

            var client = new RegisteredClient
            {
                ClientId = clientInfo.ClientId,
                ConnectionId = Context.ConnectionId,
                SupportedWorkTypes = clientInfo.SupportedWorkTypes ?? new List<string>(),
                RegisteredWorkers = clientInfo.RegisteredWorkers,
                ConnectedAt = DateTime.UtcNow,
                LastHeartbeat = DateTime.UtcNow
            };

            await _clientRegistry.RegisterClientAsync(client);

            await Clients.Caller.SendAsync("RegistrationConfirmed", new
            {
                Success = true,
                Message = $"Client {clientInfo.ClientId} registered successfully"
            });

            _logger.LogInformation("Client registered successfully: {ClientId}", clientInfo.ClientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering client {ClientId}", clientInfo.ClientId);

            await Clients.Caller.SendAsync("RegistrationFailed", new
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Unregisters a client from the BPMN engine
    /// </summary>
    /// <param name="clientId">The client ID to unregister</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task UnregisterClient(string clientId)
    {
        try
        {
            _logger.LogInformation("Unregistering client: {ClientId} from connection {ConnectionId}",
                clientId, Context.ConnectionId);

            await _clientRegistry.UnregisterClientAsync(Context.ConnectionId);

            await Clients.Caller.SendAsync("UnregistrationConfirmed", new
            {
                Success = true,
                Message = $"Client {clientId} unregistered successfully"
            });

            _logger.LogInformation("Client unregistered successfully: {ClientId}", clientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unregistering client {ClientId}", clientId);

            await Clients.Caller.SendAsync("UnregistrationFailed", new
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Handles heartbeat from clients
    /// </summary>
    /// <param name="clientId">The client ID</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task Heartbeat(string clientId)
    {
        try
        {
            await _clientRegistry.UpdateClientHeartbeatAsync(Context.ConnectionId);
            _logger.LogDebug("Heartbeat received from client: {ClientId}", clientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing heartbeat from client {ClientId}", clientId);
        }
    }

    /// <summary>
    /// Handles work completion notifications from clients
    /// </summary>
    /// <param name="workItemId">The work item ID</param>
    /// <param name="result">The work result</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task WorkCompleted(Guid workItemId, object result)
    {
        try
        {
            _logger.LogInformation("Work completed: {WorkItemId} from connection {ConnectionId}",
                workItemId, Context.ConnectionId);

            var client = await _clientRegistry.GetClientByConnectionIdAsync(Context.ConnectionId);
            if (client != null)
            {
                // TODO: Handle work completion - update process state, continue workflow, etc.
                _logger.LogInformation("Work {WorkItemId} completed by client {ClientId}",
                    workItemId, client.ClientId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing work completion for {WorkItemId}", workItemId);
        }
    }

    /// <summary>
    /// Handles work failure notifications from clients
    /// </summary>
    /// <param name="workItemId">The work item ID</param>
    /// <param name="error">The error details</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task WorkFailed(Guid workItemId, string error)
    {
        try
        {
            _logger.LogWarning("Work failed: {WorkItemId} from connection {ConnectionId}. Error: {Error}",
                workItemId, Context.ConnectionId, error);

            var client = await _clientRegistry.GetClientByConnectionIdAsync(Context.ConnectionId);
            if (client != null)
            {
                // TODO: Handle work failure - retry logic, error handling, etc.
                _logger.LogWarning("Work {WorkItemId} failed by client {ClientId}: {Error}",
                    workItemId, client.ClientId, error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing work failure for {WorkItemId}", workItemId);
        }
    }

    /// <summary>
    /// Handles service task completion from clients
    /// Receives dictionary with string keys and string values
    /// </summary>
    /// <param name="workerId">The worker ID</param>
    /// <param name="result">Dictionary with string keys and string values - passed exactly as provided</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task ServiceTaskCompleted(Guid workerId, Dictionary<string, string>? result = null)
    {
        try
        {
            _logger.LogInformation("Worker completed: {WorkerId} from connection {ConnectionId}",
                workerId, Context.ConnectionId);

            // Pass the result dictionary directly to the communication service
            await _clientCommunication.NotifyWorkerCompletedAsync(workerId, result, Context.ConnectionId);

            // Send completion acknowledgment back to the client
            var ackResult = new Dictionary<string, string>
            {
                ["status"] = "completed",
                ["workerId"] = workerId.ToString(),
                ["timestamp"] = DateTime.UtcNow.ToString("O")
            };

            await Clients.Caller.SendAsync("ServiceTaskCompletedAck", workerId, ackResult);

            _logger.LogInformation("Worker {WorkerId} completion processed successfully and acknowledgment sent", workerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing worker completion for {WorkerId}", workerId);
        }
    }

    /// <summary>
    /// Handles service task failure from clients
    /// </summary>
    /// <param name="executionId">The service task execution ID</param>
    /// <param name="error">The error details</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task ServiceTaskFailed(Guid executionId, string error)
    {
        try
        {
            _logger.LogWarning("Service task failed: {ExecutionId} from connection {ConnectionId}. Error: {Error}",
                executionId, Context.ConnectionId, error);

            var client = await _clientRegistry.GetClientByConnectionIdAsync(Context.ConnectionId);
            if (client != null)
            {
                // TODO: Handle service task failure - this might cause the process to fail
                // or retry the task, depending on the error handling strategy
                _logger.LogWarning("Service task {ExecutionId} failed by client {ClientId}: {Error}",
                    executionId, client.ClientId, error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing service task failure for {ExecutionId}", executionId);
        }
    }
}