using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.Repositories;

namespace Novin.Bpmn.Engine.Application.Features.Deployments.Commands.CreateDeployment;

public sealed class CreateDeploymentCommandHandler : IRequestHandler<CreateDeploymentCommand, CreateDeploymentResult>
{
    private readonly IUnitOfWork _uow;
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly ILogger<CreateDeploymentCommandHandler> _logger;

    public CreateDeploymentCommandHandler(
        IUnitOfWork uow,
        IDeploymentRepository deploymentRepository,
        ILogger<CreateDeploymentCommandHandler> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _deploymentRepository = deploymentRepository ?? throw new ArgumentNullException(nameof(deploymentRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CreateDeploymentResult> Handle(CreateDeploymentCommand request, CancellationToken ct)
    {
        if (request.ProjectId == Guid.Empty)
            throw new ArgumentException("ProjectId cannot be empty", nameof(request));

        if (string.IsNullOrWhiteSpace(request.DeploymentKey))
            throw new ArgumentException("DeploymentKey is required", nameof(request));

        if (string.IsNullOrWhiteSpace(request.BpmnXml))
            throw new ArgumentException("BpmnXml is required", nameof(request));

        Deployment? created = null;

        await _uow.ExecuteInTransactionAsync(async trxCt =>
        {
            created = Deployment.Create(
                projectId: request.ProjectId,
                deploymentKey: request.DeploymentKey.Trim(),
                bpmnXml: request.BpmnXml,
                label: string.IsNullOrWhiteSpace(request.Label) ? null : request.Label.Trim());

            await _deploymentRepository.AddAsync(created, trxCt);

            _logger.LogInformation(
                "Deployment created. DeploymentId={DeploymentId} Key={Key} ProjectId={ProjectId} Version={Version}",
                created.Id, created.DeploymentKey, created.ProjectId, created.Version);
        }, ct);

        // Note: Definition cache will be warmed up by DeploymentCreatedEventHandler (via Outbox)

        return new CreateDeploymentResult(
            created!.Id,
            created.ProjectId,
            created.DeploymentKey,
            created.Label,
            created.Version,
            created.DeployedAtUtc,
            created.IsActive);
    }
}

