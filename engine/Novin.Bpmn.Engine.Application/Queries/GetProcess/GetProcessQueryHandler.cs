using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;

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

        return new ProcessDto
        {
            Id = process.Id,
            Name = process.Name,
            ProcessDefinitionId = process.ProcessDefinitionId,
            State = process.State,
            Variables = new Dictionary<string, object>(process.Variables),
            CreatedAt = process.CreatedAt,
            StartedAt = process.StartedAt,
            CompletedAt = process.CompletedAt
        };
    }
}

