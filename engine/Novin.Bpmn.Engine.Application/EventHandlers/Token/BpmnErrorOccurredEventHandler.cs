using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.Events;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using ProcessEntity = Novin.Bpmn.Engine.Domain.Entities.Process;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

/// <summary>
/// Handles BPMN error occurrences by attempting boundary event handling
/// or escalating to process-level error handling
/// </summary>
public sealed class BpmnErrorOccurredEventHandler : INotificationHandler<BpmnErrorOccurredEvent>
{
    private readonly IUnitOfWork _uow;
    private readonly IIncidentService _incidentService;
    private readonly ILogger<BpmnErrorOccurredEventHandler> _logger;

    public BpmnErrorOccurredEventHandler(
        IUnitOfWork uow,
        IIncidentService incidentService,
        ILogger<BpmnErrorOccurredEventHandler> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _incidentService = incidentService ?? throw new ArgumentNullException(nameof(incidentService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async System.Threading.Tasks.Task Handle(BpmnErrorOccurredEvent @event, CancellationToken ct)
    {
        _logger.LogWarning(
            "[BPMN-ERROR] Handling BPMN error. ProcessId={ProcessId} TokenId={TokenId} ElementId={ElementId} ErrorCode={ErrorCode} Message={Message}",
            @event.ProcessId,
            @event.TokenId,
            @event.ElementId,
            @event.ErrorCode,
            @event.ErrorMessage);

        await _uow.ExecuteInTransactionAsync(async trxCt =>
        {
            var process = await _uow.Processes.GetByIdAsync(@event.ProcessId, trxCt);
            if (process == null)
            {
                _logger.LogWarning("[BPMN-ERROR] Process not found. ProcessId={ProcessId}", @event.ProcessId);
                return;
            }

            var token = await _uow.Tokens.GetByIdAsync(@event.TokenId, trxCt);
            if (token == null)
            {
                _logger.LogWarning("[BPMN-ERROR] Token not found. TokenId={TokenId}", @event.TokenId);
                return;
            }

            // Publish ErrorRaisedEvent to trigger boundary event handling
            var errorRaisedEvent = new ErrorRaisedEvent(
                ProcessId: @event.ProcessId,
                TokenId: @event.TokenId,
                ElementId: @event.ElementId,
                ErrorCode: @event.ErrorCode,
                ErrorMessage: @event.ErrorMessage,
                ScopeId: @event.ScopeId,
                OccurredAtUtc: DateTime.UtcNow);

            // The ErrorRaisedEvent will be handled by BoundarySubscriptionManager
            // If no boundary handler is found, the error will remain unhandled
            // and the token will still be at the same element after boundary processing

            // Publish the error event through the mediator (this will be dispatched by UnitOfWork)

            // Reload token to check if error was handled
            token = await _uow.Tokens.GetByIdAsync(@event.TokenId, trxCt);
            if (token == null)
            {
                _logger.LogWarning("[BPMN-ERROR] Token not found after error handling. TokenId={TokenId}", @event.TokenId);
                return;
            }

            // Check if error was handled by boundary event
            var wasHandled = token.CurrentElementId != @event.ElementId
                             || token.State == TokenState.Terminated
                             || token.State == TokenState.Completed;

            if (!wasHandled)
            {
                // Error was not handled - escalate to process level
                _logger.LogWarning(
                    "[BPMN-ERROR] BPMN error not handled by boundary events. Escalating to process level. ProcessId={ProcessId} TokenId={TokenId} ErrorCode={ErrorCode}",
                    @event.ProcessId,
                    @event.TokenId,
                    @event.ErrorCode);

                await EscalateUnhandledBpmnErrorAsync(process, token, @event, trxCt);
            }
            else
            {
                _logger.LogInformation(
                    "[BPMN-ERROR] BPMN error handled successfully by boundary event. ProcessId={ProcessId} TokenId={TokenId} ErrorCode={ErrorCode}",
                    @event.ProcessId,
                    @event.TokenId,
                    @event.ErrorCode);
            }
        }, ct);
    }

    private async System.Threading.Tasks.Task EscalateUnhandledBpmnErrorAsync(
        ProcessEntity process,
        Token token,
        BpmnErrorOccurredEvent @event,
        CancellationToken ct)
    {
        // Step 1: Convert all active executable tokens to trace tokens
        var allTokens = await _uow.Tokens.GetByProcessIdAsync(process.Id, ct);
        var executableTokens = allTokens
            .Where(t => t.IsExecutable && TokenExtensions.IsActiveToken(t))
            .ToList();

        _logger.LogInformation(
            "[BPMN-ERROR] Converting {Count} executable tokens to trace tokens for unhandled BPMN error. ProcessId={ProcessId}",
            executableTokens.Count,
            process.Id);

        foreach (var t in executableTokens)
        {
            _logger.LogDebug(
                "[BPMN-ERROR] Converting token to trace token. TokenId={TokenId} ElementId={ElementId}",
                t.Id,
                t.CurrentElementId);

            t.MarkNonExecutable($"Unhandled BPMN error: {@event.ErrorCode} - converted to trace token");

            if (t.State == TokenState.Waiting)
            {
                t.ResumeWithoutProcessing();
            }
        }

        // Step 2: Cancel all boundary subscriptions
        var allSubscriptions = await _uow.BoundarySubscriptions.GetByProcessIdAsync(process.Id, ct);
        var activeSubscriptions = allSubscriptions
            .Where(s => s.State == SubscriptionState.Active)
            .ToList();

        _logger.LogInformation(
            "[BPMN-ERROR] Canceling {Count} active subscriptions for unhandled error. ProcessId={ProcessId}",
            activeSubscriptions.Count,
            process.Id);

        foreach (var sub in activeSubscriptions)
        {
            sub.Cancel($"Unhandled BPMN error: {@event.ErrorCode}");
            await _uow.BoundarySubscriptions.UpdateAsync(sub, ct);
        }

        // Step 3: Create incident and fail the token
        var incident = await _incidentService.CreateBpmnErrorAsync(
            process.Id,
            token.Id,
            token.CurrentElementId,
            @event.ErrorCode,
            @event.ErrorMessage,
            ct);

        // Fail the token
        token.Fail(
            $"Unhandled BPMN Error: {@event.ErrorCode} - {@event.ErrorMessage}",
            ErrorType.BpmnError,
            errorCode: @event.ErrorCode,
            incidentId: incident.Id);

        // Fail the process
        process.HandleUnhandledBpmnError(@event.ErrorCode, @event.ErrorMessage);

        _logger.LogInformation(
            "[BPMN-ERROR] Unhandled BPMN error escalated to process level. ProcessId={ProcessId} TokenId={TokenId} IncidentId={IncidentId}",
            process.Id,
            token.Id,
            incident.Id);
    }

}