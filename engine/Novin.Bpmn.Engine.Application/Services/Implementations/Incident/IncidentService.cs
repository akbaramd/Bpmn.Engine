using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.Services;

public sealed class IncidentService : IIncidentService
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<IncidentService> _logger;

    public IncidentService(
        IUnitOfWork uow,
        ILogger<IncidentService> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Incident> CreateTechnicalFailureAsync(
        Guid processId,
        Guid tokenId,
        string elementId,
        string message,
        string? stackTrace = null,
        CancellationToken ct = default)
    {
        var incident = new Incident(
            processId,
            tokenId,
            elementId,
            ErrorType.TechnicalFailure,
            message,
            errorCode: null,
            stackTrace);

        await _uow.Incidents.AddAsync(incident, ct);
        
            _logger.LogInformation(
            "[INCIDENT] Technical failure incident created. IncidentId={IncidentId} ProcessId={ProcessId} TokenId={TokenId} ElementId={ElementId}",
                incident.Id,
                processId,
                tokenId,
                elementId);

        return incident;
    }

    public async Task<Incident> CreateBpmnErrorAsync(
        Guid processId,
        Guid tokenId,
        string elementId,
        string errorCode,
        string message,
        CancellationToken ct = default)
    {
        var incident = new Incident(
            processId,
            tokenId,
            elementId,
            ErrorType.BpmnError,
            message,
            errorCode,
            stackTrace: null);

        await _uow.Incidents.AddAsync(incident, ct);
        
            _logger.LogInformation(
            "[INCIDENT] BPMN error incident created. IncidentId={IncidentId} ProcessId={ProcessId} TokenId={TokenId} ElementId={ElementId} ErrorCode={ErrorCode}",
                incident.Id,
                processId,
                tokenId,
                elementId,
                errorCode);

        return incident;
    }

    public async Task RetryIncidentAsync(Guid incidentId, CancellationToken ct = default)
    {
        await _uow.ExecuteInTransactionAsync(async trxCt =>
        {
            var incident = await _uow.Incidents.GetByIdAsync(incidentId, trxCt);
            if (incident == null)
            {
                _logger.LogWarning("[INCIDENT] Incident not found for retry. IncidentId={IncidentId}", incidentId);
                return;
            }

            incident.Retry();
            await _uow.Incidents.UpdateAsync(incident, trxCt);

            _logger.LogInformation(
                "[INCIDENT] Incident retried. IncidentId={IncidentId} Retries={Retries}",
                incidentId,
                incident.Retries);
        }, ct);
    }

    public async Task ResolveIncidentAsync(Guid incidentId, CancellationToken ct = default)
    {
        await _uow.ExecuteInTransactionAsync(async trxCt =>
        {
            var incident = await _uow.Incidents.GetByIdAsync(incidentId, trxCt);
            if (incident == null)
            {
                _logger.LogWarning("[INCIDENT] Incident not found for resolve. IncidentId={IncidentId}", incidentId);
                return;
            }

            incident.Resolve();
            await _uow.Incidents.UpdateAsync(incident, trxCt);

            _logger.LogInformation(
                "[INCIDENT] Incident resolved. IncidentId={IncidentId}",
                incidentId);
        }, ct);
    }
}

