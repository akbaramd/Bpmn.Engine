using MediatR;
using Novin.Bpmn.Engine.Application.Queries.GetIncidents;

namespace Novin.Bpmn.Engine.Application.Queries.GetIncidents;

/// <summary>
/// Query برای دریافت Incident های یک Process
/// </summary>
public sealed record GetIncidentsQuery(Guid ProcessId) : IRequest<IEnumerable<IncidentDto>>;