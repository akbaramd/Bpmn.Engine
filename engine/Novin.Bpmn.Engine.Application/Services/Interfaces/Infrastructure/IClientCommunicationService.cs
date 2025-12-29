using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Models;

namespace Novin.Bpmn.Engine.Application.Services;

/// <summary>
/// Service for communicating with external BPMN clients
/// </summary>
public interface IClientCommunicationService
{
    /// <summary>
    /// Routes a worker to appropriate client(s)
    /// </summary>
    /// <param name="worker">The worker to route</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task RouteServiceTaskToClientsAsync(Job? worker, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies that a worker has been completed
    /// </summary>
    /// <param name="workerId">The worker ID</param>
    /// <param name="result">The execution result (dictionary with string keys and string values)</param>
    /// <param name="completedBy">ID of the user/client that completed the worker</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task NotifyWorkerCompletedAsync(Guid workerId, Dictionary<string, string>? result = null, string? completedBy = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies that a worker has failed
    /// </summary>
    /// <param name="workerId">The worker ID</param>
    /// <param name="error">The error message</param>
    /// <param name="completedBy">ID of the user/client that failed the worker</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task NotifyWorkerFailedAsync(Guid workerId, string error, string? completedBy = null, CancellationToken cancellationToken = default);
}