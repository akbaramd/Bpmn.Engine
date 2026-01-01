using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.DomainServices;
using Novin.Bpmn.Engine.Domain.Events;
using Novin.Bpmn.Engine.Domain.Repositories;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.EventHandlers.Deployment;

/// <summary>
/// Handler for DeploymentCreatedEvent: Warm up definitions in memory.
/// Runs AFTER transaction commit (via Outbox).
/// </summary>
public sealed class DeploymentCreatedEventHandler : INotificationHandler<DeploymentCreatedEvent>
{
    private readonly IExecutableDefinitionCatalog _catalog;
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly IBpmnQuery _bpmnQuery;
    private readonly ILogger<DeploymentCreatedEventHandler> _logger;

    public DeploymentCreatedEventHandler(
        IExecutableDefinitionCatalog catalog,
        IDeploymentRepository deploymentRepository,
        IBpmnQuery bpmnQuery,
        ILogger<DeploymentCreatedEventHandler> logger)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _deploymentRepository = deploymentRepository ?? throw new ArgumentNullException(nameof(deploymentRepository));
        _bpmnQuery = bpmnQuery ?? throw new ArgumentNullException(nameof(bpmnQuery));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(DeploymentCreatedEvent notification, CancellationToken ct)
    {
        try
        {
            var deployment = await _deploymentRepository.GetByIdAsync(notification.DeploymentId, ct);
            if (deployment is null)
            {
                _logger.LogWarning("Deployment {DeploymentId} not found for warm-up", notification.DeploymentId);
                return;
            }

            // Get all processes in this deployment
            var processes = _bpmnQuery.GetAllProcesses(deployment);
            foreach (var process in processes)
            {
                var defRef = new ProcessDefinitionRef(
                    deployment.Id,
                    process.id,
                    deployment.Version);

                // Load and cache
                await _catalog.GetAsync(defRef, ct);
                _logger.LogInformation("Warmed up definition: {Ref}", defRef.ToCacheKey());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to warm up deployment {DeploymentId}", notification.DeploymentId);
            // Don't throw - deployment is already created
        }
    }
}

