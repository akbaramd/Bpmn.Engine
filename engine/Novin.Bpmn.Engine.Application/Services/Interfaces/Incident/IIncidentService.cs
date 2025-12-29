using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.Services;

/// <summary>
/// Service برای مدیریت Incident lifecycle
/// </summary>
public interface IIncidentService
{
    Task<Incident> CreateTechnicalFailureAsync(
        Guid processId,
        Guid tokenId,
        Guid? nodeInstanceId,
        Guid? workerId,
        string elementId,
        string message,
        string? stackTrace = null,
        CancellationToken ct = default);

    Task<Incident> CreateBpmnErrorAsync(
        Guid processId,
        Guid tokenId,
        Guid? nodeInstanceId,
        Guid? workerId,
        string elementId,
        string errorCode,
        string message,
        CancellationToken ct = default);

    Task RetryIncidentAsync(Guid incidentId, CancellationToken ct = default);
    Task ResolveIncidentAsync(Guid incidentId, string? note = null, CancellationToken ct = default);
}