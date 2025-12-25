using MediatR;
using Novin.Bpmn.Engine.Application.Queries.GetIncidents;

namespace Novin.Bpmn.Engine.Application.Queries.GetIncidents;

/// <summary>
/// Query برای دریافت Incident های یک Process
/// </summary>
public sealed record GetIncidentsQuery(Guid ProcessId) : IRequest<IEnumerable<IncidentDto>>;

/// <summary>
/// DTO برای Incident
/// </summary>
public sealed record IncidentDto
{
    public Guid Id { get; init; }
    public Guid ProcessId { get; init; }
    public Guid TokenId { get; init; }
    public string ElementId { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string? ErrorCode { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? StackTrace { get; init; }
    public string Status { get; init; } = string.Empty;
    public int Retries { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime LastOccurredAt { get; init; }
    public DateTime? ResolvedAt { get; init; }
}

