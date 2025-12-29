using MediatR;
using Novin.Bpmn.Engine.Application.Common.Interfaces;

namespace Novin.Bpmn.Engine.Application.Queries.GetDeployment;

public class GetDeploymentQueryHandler : IRequestHandler<GetDeploymentQuery, DeploymentDto?>
{
    private readonly IDeploymentRepository _deploymentRepository;

    public GetDeploymentQueryHandler(IDeploymentRepository deploymentRepository)
    {
        _deploymentRepository = deploymentRepository ?? throw new ArgumentNullException(nameof(deploymentRepository));
    }

    public async Task<DeploymentDto?> Handle(GetDeploymentQuery request, CancellationToken cancellationToken)
    {
        var deployment = await _deploymentRepository.GetByIdAsync(request.DeploymentId);
        if (deployment == null)
            return null;

        return new DeploymentDto(
            deployment.Id,
            deployment.DeploymentKey,
            deployment.Label,
            deployment.Version,
            deployment.BpmnXml,
            deployment.DeployedAtUtc,
            deployment.IsActive
        );
    }
}