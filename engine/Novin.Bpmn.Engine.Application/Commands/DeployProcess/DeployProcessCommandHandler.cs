using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Application.Commands.DeployProcess;

public class DeployProcessCommandHandler : IRequestHandler<DeployProcessCommand, DeployProcessResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeployProcessCommandHandler> _logger;

    public DeployProcessCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<DeployProcessCommandHandler> logger)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DeployProcessResult> Handle(DeployProcessCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deploying process with key: {DeploymentKey}", request.DeploymentKey);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            // Check if deployment with this key already exists
            var existingDeployment = await _unitOfWork.Deployments.GetLatestByDeploymentKeyAsync(
                request.DeploymentKey, cancellationToken);

            Deployment deployment;
            bool isNewDeployment;
            int version;

            if (existingDeployment != null)
            {
                // Auto-increment version for existing deployment key
                version = await _unitOfWork.Deployments.GetNextVersionAsync(request.DeploymentKey, cancellationToken);
                isNewDeployment = false;
                
                _logger.LogInformation("Deployment key {DeploymentKey} already exists. Creating new version: {Version}", 
                    request.DeploymentKey, version);
            }
            else
            {
                // First deployment with this key
                version = 1;
                isNewDeployment = true;
                
                _logger.LogInformation("Creating new deployment with key: {DeploymentKey}", request.DeploymentKey);
            }

            // Create new deployment with appropriate version
            deployment = new Deployment(
                request.DeploymentKey,
                request.BpmnXml,
                version,
                request.Label);

            await _unitOfWork.Deployments.AddAsync(deployment, cancellationToken);
         
            // Track aggregate for event dispatching on commit
            
            // Commit transaction (this will dispatch events)
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            _logger.LogInformation("Deployment created successfully. DeploymentId: {DeploymentId}, Version: {Version}", 
                deployment.Id, deployment.Version);

            return new DeployProcessResult
            {
                DeploymentId = deployment.Id,
                DeploymentKey = deployment.DeploymentKey,
                Version = deployment.Version,
                DeployedAt = deployment.DeployedAt,
                IsNewDeployment = isNewDeployment
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deploying process with key: {DeploymentKey}", request.DeploymentKey);
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}

