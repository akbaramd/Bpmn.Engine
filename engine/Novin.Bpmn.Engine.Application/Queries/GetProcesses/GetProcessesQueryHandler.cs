using MediatR;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.Queries.GetProcesses;

public class GetProcessesQueryHandler : IRequestHandler<GetProcessesQuery, IEnumerable<ProcessDto>>
{
    private readonly IProcessRepository _processRepository;

    public GetProcessesQueryHandler(IProcessRepository processRepository)
    {
        _processRepository = processRepository ?? throw new ArgumentNullException(nameof(processRepository));
    }

    public async Task<IEnumerable<ProcessDto>> Handle(GetProcessesQuery request, CancellationToken cancellationToken)
    {
        var processes = await _processRepository.GetAllAsync();

        // Apply filters
        if (request.State.HasValue)
        {
            processes = processes.Where(p => p.State == request.State.Value);
        }

        if (request.DeploymentId.HasValue)
        {
            processes = processes.Where(p => p.DeploymentId == request.DeploymentId.Value);
        }

        if (!string.IsNullOrEmpty(request.ProcessBpmnId))
        {
            processes = processes.Where(p => p.ProcessBpmnId == request.ProcessBpmnId);
        }

        return processes
            .OrderByDescending(p => p.CreatedAtUtc)
            .Skip(request.Skip)
            .Take(request.Take)
            .Select(p => new ProcessDto(
                p.Id,
                p.Name,
                p.DeploymentId,
                p.ProcessBpmnId,
                p.State,
                p.CreatedAtUtc,
                p.StartedAtUtc,
                p.CompletedAtUtc,
                p.VariablesObject
            ));
    }
}