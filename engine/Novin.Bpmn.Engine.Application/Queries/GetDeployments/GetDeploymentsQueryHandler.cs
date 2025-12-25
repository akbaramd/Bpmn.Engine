using MediatR;
using Novin.Bpmn.Engine.Application.Common.Interfaces;

namespace Novin.Bpmn.Engine.Application.Queries.GetDeployments;

public class GetDeploymentsQueryHandler : IRequestHandler<GetDeploymentsQuery, IEnumerable<DeploymentDto>>
{
    private readonly IDeploymentRepository _deploymentRepository;

    public GetDeploymentsQueryHandler(IDeploymentRepository deploymentRepository)
    {
        _deploymentRepository = deploymentRepository ?? throw new ArgumentNullException(nameof(deploymentRepository));
    }

    public async Task<IEnumerable<DeploymentDto>> Handle(GetDeploymentsQuery request, CancellationToken cancellationToken)
    {
        var deployments = await _deploymentRepository.GetAllAsync();

        // Apply filters
        if (!string.IsNullOrEmpty(request.DeploymentKey))
        {
            deployments = deployments.Where(d => d.DeploymentKey == request.DeploymentKey);
        }

        if (request.ActiveOnly)
        {
            deployments = deployments.Where(d => d.IsActive);
        }

        return deployments
            .OrderByDescending(d => d.Version)
            .Select(d => new DeploymentDto(
                d.Id,
                d.DeploymentKey,
                d.Label,
                d.Version,
                d.BpmnXml,
                d.DeployedAt,
                d.IsActive
            ));
    }
}