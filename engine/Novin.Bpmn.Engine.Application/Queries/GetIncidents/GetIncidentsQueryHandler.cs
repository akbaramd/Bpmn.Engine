using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Queries.GetIncidents;

namespace Novin.Bpmn.Engine.Application.Queries.GetIncidents;

public sealed class GetIncidentsQueryHandler : IRequestHandler<GetIncidentsQuery, IEnumerable<IncidentDto>>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<GetIncidentsQueryHandler> _logger;

    public GetIncidentsQueryHandler(
        IUnitOfWork uow,
        ILogger<GetIncidentsQueryHandler> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IEnumerable<IncidentDto>> Handle(GetIncidentsQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting incidents for process: {ProcessId}", request.ProcessId);

        var incidents = await _uow.Incidents.GetByProcessIdAsync(request.ProcessId, cancellationToken);

        return incidents.Select(i => new IncidentDto
        {
            Id = i.Id,
            ProcessId = i.ProcessId,
            TokenId = i.TokenId,
            ElementId = i.ElementId,
            Type = i.Type.ToString(),
            ErrorCode = i.ErrorCode,
            Message = i.Message,
            StackTrace = i.StackTrace,
            Status = i.Status.ToString(),
            Retries = i.Retries,
            CreatedAt = i.CreatedAt,
            LastOccurredAt = i.LastOccurredAt,
            ResolvedAt = i.ResolvedAt
        });
    }
}

