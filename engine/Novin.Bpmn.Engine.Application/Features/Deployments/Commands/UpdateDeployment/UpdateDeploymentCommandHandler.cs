using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.Repositories;

namespace Novin.Bpmn.Engine.Application.Features.Deployments.Commands.UpdateDeployment;

public sealed class UpdateDeploymentCommandHandler : IRequestHandler<UpdateDeploymentCommand, UpdateDeploymentResult>
{
    private readonly IUnitOfWork _uow;
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly ILogger<UpdateDeploymentCommandHandler> _logger;

    public UpdateDeploymentCommandHandler(
        IUnitOfWork uow,
        IDeploymentRepository deploymentRepository,
        ILogger<UpdateDeploymentCommandHandler> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _deploymentRepository = deploymentRepository ?? throw new ArgumentNullException(nameof(deploymentRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<UpdateDeploymentResult> Handle(UpdateDeploymentCommand request, CancellationToken ct)
    {
        Deployment? result = null;
        var isNewVersion = false;

        await _uow.ExecuteInTransactionAsync(async trxCt =>
        {
            var existing = await _deploymentRepository.GetByIdAsync(request.DeploymentId, trxCt);
            if (existing is null)
                throw new InvalidOperationException($"Deployment {request.DeploymentId} not found");

            // Versioning logic: if requested version > current version, create new version
            if (request.RequestedVersion.HasValue && 
                request.RequestedVersion.Value > existing.Version &&
                !string.IsNullOrWhiteSpace(request.BpmnXml))
            {
                // Create new version
                result = existing.CreateNextVersion(
                    newBpmnXml: request.BpmnXml,
                    newLabel: request.Label);

                // Optionally deactivate old version
                existing.Deactivate("New version created");

                await _deploymentRepository.AddAsync(result, trxCt);
                await _deploymentRepository.UpdateAsync(existing, trxCt);

                isNewVersion = true;

                _logger.LogInformation(
                    "New deployment version created. OldVersion={OldVersion} NewVersion={NewVersion} DeploymentKey={Key}",
                    existing.Version, result.Version, result.DeploymentKey);
            }
            else
            {
                // Update existing deployment
                if (!string.IsNullOrWhiteSpace(request.BpmnXml))
                    existing.UpdateBpmnXml(request.BpmnXml);

                if (!string.IsNullOrWhiteSpace(request.Label))
                    existing.UpdateLabel(request.Label.Trim());

                await _deploymentRepository.UpdateAsync(existing, trxCt);
                result = existing;

                _logger.LogInformation(
                    "Deployment updated. DeploymentId={DeploymentId} Version={Version}",
                    result.Id, result.Version);
            }
        }, ct);

        // Note: Definition cache will be invalidated/reloaded by DeploymentUpdatedEventHandler or DeploymentCreatedEventHandler (via Outbox)

        return new UpdateDeploymentResult(
            result!.Id,
            result.ProjectId,
            result.DeploymentKey,
            result.Label,
            result.Version,
            result.DeployedAtUtc,
            result.IsActive,
            isNewVersion);
    }
}

