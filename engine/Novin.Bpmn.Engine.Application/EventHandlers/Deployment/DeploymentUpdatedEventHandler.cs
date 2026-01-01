using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.DomainServices;
using Novin.Bpmn.Engine.Domain.Events;
using Novin.Bpmn.Engine.Domain.Repositories;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.EventHandlers.Deployment;

/// <summary>
/// Handler for DeploymentUpdatedEvent: Invalidate and reload definitions.
/// Runs AFTER transaction commit (via Outbox).
/// </summary>
public sealed class DeploymentUpdatedEventHandler : INotificationHandler<DeploymentUpdatedEvent>
{
    private readonly IExecutableDefinitionCatalog _catalog;
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly IBpmnQuery _bpmnQuery;
    private readonly ILogger<DeploymentUpdatedEventHandler> _logger;

    public DeploymentUpdatedEventHandler(
        IExecutableDefinitionCatalog catalog,
        IDeploymentRepository deploymentRepository,
        IBpmnQuery bpmnQuery,
        ILogger<DeploymentUpdatedEventHandler> logger)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _deploymentRepository = deploymentRepository ?? throw new ArgumentNullException(nameof(deploymentRepository));
        _bpmnQuery = bpmnQuery ?? throw new ArgumentNullException(nameof(bpmnQuery));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(DeploymentUpdatedEvent notification, CancellationToken ct)
    {
        try
        {
            var deployment = await _deploymentRepository.GetByIdAsync(notification.DeploymentId, ct);
            if (deployment is null)
            {
                _logger.LogWarning("Deployment {DeploymentId} not found for invalidation", notification.DeploymentId);
                return;
            }

            // Invalidate all processes in this specific deployment (by ID, not by key)
            // This handles the case where a deployment was updated (not versioned)
            var processes = _bpmnQuery.GetAllProcesses(deployment);
            foreach (var process in processes)
            {
                var defRef = new ProcessDefinitionRef(
                    deployment.Id,
                    process.id,
                    deployment.Version);

                await _catalog.OnDefinitionChangedAsync(defRef, ct);
                _logger.LogInformation("Invalidated and reloaded definition: {Ref}", defRef.ToCacheKey());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to invalidate deployment {DeploymentId}", notification.DeploymentId);
            // Don't throw - deployment is already updated
        }
    }
}

