using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services;

namespace Novin.Bpmn.Engine.Application.Queries.GetProcess;

public class GetProcessQueryHandler : IRequestHandler<GetProcessQuery, ProcessDto?>
{
    private readonly IProcessRepository _processRepository;
    private readonly ILogger<GetProcessQueryHandler> _logger;

    public GetProcessQueryHandler(
        IProcessRepository processRepository,
        ILogger<GetProcessQueryHandler> logger)
    {
        _processRepository = processRepository ?? throw new ArgumentNullException(nameof(processRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ProcessDto?> Handle(GetProcessQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting process: {ProcessId}", request.ProcessId);

        var process = await _processRepository.GetByIdAsync(request.ProcessId, cancellationToken);
        
        if (process == null)
        {
            _logger.LogWarning("Process not found: {ProcessId}", request.ProcessId);
            return null;
        }

        // Calculate derived status (considers open incidents, failed tokens, etc.)

        return new ProcessDto
        {
            Id = process.Id,
            Name = process.Name,
            DeploymentId = process.DeploymentId,
            ProcessBpmnId = process.ProcessBpmnId,
            State = process.State,
            Variables = new Dictionary<string, string>(process.Variables),
            CreatedAt = process.CreatedAtUtc,
            StartedAt = process.StartedAtUtc,
            CompletedAt = process.CompletedAtUtc
        };
    }
}

