// File: Novin.Bpmn.Engine.Application/Services/IncidentService.cs
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.Services;

public sealed class IncidentService : IIncidentService
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<IncidentService> _logger;

    public IncidentService(IUnitOfWork uow, ILogger<IncidentService> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Incident> CreateTechnicalFailureAsync(
        Guid processId,
        Guid tokenId,
        Guid? nodeInstanceId,
        Guid? workerId,
        string elementId,
        string message,
        string? stackTrace = null,
        CancellationToken ct = default)
    {
        var inc = Incident.Open(
            processId: processId,
            tokenId: tokenId,
            nodeInstanceId: nodeInstanceId,
            workerId: workerId,
            elementId: elementId,
            scope: nodeInstanceId.HasValue ? IncidentScope.Node : IncidentScope.Token,
            cause: IncidentCause.TechnicalFailure,
            message: message,
            errorCode: null,
            stackTrace: stackTrace);

        await _uow.Incidents.AddAsync(inc, ct);

        _logger.LogInformation(
            "[INCIDENT] TechnicalFailure opened. IncidentId={IncidentId} P={ProcessId} T={TokenId} N={NodeId} W={WorkerId} E={ElementId}",
            inc.Id, processId, tokenId, nodeInstanceId, workerId, elementId);

        return inc;
    }

    public async Task<Incident> CreateBpmnErrorAsync(
        Guid processId,
        Guid tokenId,
        Guid? nodeInstanceId,
        Guid? workerId,
        string elementId,
        string errorCode,
        string message,
        CancellationToken ct = default)
    {
        var inc = Incident.Open(
            processId: processId,
            tokenId: tokenId,
            nodeInstanceId: nodeInstanceId,
            workerId: workerId,
            elementId: elementId,
            scope: nodeInstanceId.HasValue ? IncidentScope.Node : IncidentScope.Token,
            cause: IncidentCause.BpmnError,
            message: message,
            errorCode: errorCode,
            stackTrace: null);

        await _uow.Incidents.AddAsync(inc, ct);

        _logger.LogInformation(
            "[INCIDENT] BpmnError opened. IncidentId={IncidentId} P={ProcessId} T={TokenId} N={NodeId} W={WorkerId} E={ElementId} Code={Code}",
            inc.Id, processId, tokenId, nodeInstanceId, workerId, elementId, errorCode);

        return inc;
    }

    public async Task RetryIncidentAsync(Guid incidentId, CancellationToken ct = default)
    {
        await _uow.ExecuteInTransactionAsync(async trxCt =>
        {
            var incident = await _uow.Incidents.GetByIdAsync(incidentId, trxCt);
            if (incident is null)
            {
                _logger.LogWarning("[INCIDENT] Not found for retry. IncidentId={IncidentId}", incidentId);
                return;
            }

            incident.Retry();
            await _uow.Incidents.UpdateAsync(incident, trxCt);

            _logger.LogInformation(
                "[INCIDENT] Retried. IncidentId={IncidentId} RetryCount={RetryCount}",
                incidentId, incident.RetryCount);
        }, ct);
    }

    public async Task ResolveIncidentAsync(Guid incidentId, string? note = null, CancellationToken ct = default)
    {
        await _uow.ExecuteInTransactionAsync(async trxCt =>
        {
            var incident = await _uow.Incidents.GetByIdAsync(incidentId, trxCt);
            if (incident is null)
            {
                _logger.LogWarning("[INCIDENT] Not found for resolve. IncidentId={IncidentId}", incidentId);
                return;
            }

            incident.Resolve(note);
            await _uow.Incidents.UpdateAsync(incident, trxCt);

            _logger.LogInformation("[INCIDENT] Resolved. IncidentId={IncidentId}", incidentId);
        }, ct);
    }
}
