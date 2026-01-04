using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.Events;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

/// <summary>
/// Handles technical failure occurrences during token processing
/// </summary>
public sealed class TechnicalFailureOccurredEventHandler : INotificationHandler<TechnicalFailureOccurredEvent>
{
    private readonly IUnitOfWork _uow;
    private readonly IIncidentService _incidentService;
    private readonly ILogger<TechnicalFailureOccurredEventHandler> _logger;

    public TechnicalFailureOccurredEventHandler(
        IUnitOfWork uow,
        IIncidentService incidentService,
        ILogger<TechnicalFailureOccurredEventHandler> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _incidentService = incidentService ?? throw new ArgumentNullException(nameof(incidentService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async System.Threading.Tasks.Task Handle(TechnicalFailureOccurredEvent @event, CancellationToken ct)
    {
        _logger.LogError(
            "[TECHNICAL-FAILURE] Handling technical failure. ProcessId={ProcessId} TokenId={TokenId} ElementId={ElementId} Error={ErrorMessage}",
            @event.ProcessId,
            @event.TokenId,
            @event.ElementId,
            @event.ErrorMessage);

        await _uow.ExecuteInTransactionAsync(async trxCt =>
        {
            var process = await _uow.Processes.GetByIdAsync(@event.ProcessId, trxCt);
            if (process == null)
            {
                _logger.LogWarning("[TECHNICAL-FAILURE] Process not found. ProcessId={ProcessId}", @event.ProcessId);
                return;
            }

            var token = await _uow.Tokens.GetByIdAsync(@event.TokenId, trxCt);
            if (token == null)
            {
                _logger.LogWarning("[TECHNICAL-FAILURE] Token not found. TokenId={TokenId}", @event.TokenId);
                return;
            }

            // Create incident for technical failure
            var incident = await _incidentService.CreateTechnicalFailureAsync(
                @event.ProcessId,
                @event.TokenId,
                null,
                null,
                @event.ElementId,
                @event.ErrorMessage,
                @event.StackTrace,
                trxCt);

            // Fail the token with the incident
            token.Fail(
                $"Technical failure: {@event.ErrorMessage}",
                EngineErrorKind.Technical,
                errorCode: null,
                incidentId: incident.Id);

            _logger.LogInformation(
                "[TECHNICAL-FAILURE] Technical failure handled. ProcessId={ProcessId} TokenId={TokenId} IncidentId={IncidentId}",
                @event.ProcessId,
                @event.TokenId,
                incident.Id);
        }, ct);
    }
}