using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Commands.NodeDispatch;
using Novin.Bpmn.Engine.Application.Commands.NodeInstances;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Events;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using NodeState = Novin.Bpmn.Engine.Domain.Entities.NodeState;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

/// <summary>
/// Handles TokenResumedEvent (after waiting) and triggers processing.
/// Resume is semantically "arrive again" at the same element.
/// 
/// ⛔ IMPORTANT: This handler only orchestrates - NO variable mapping here!
/// Mapping happens only in NodeProcessAsync at activity execution boundary.
/// </summary>
public sealed class TokenResumedEventHandler : INotificationHandler<TokenResumedEvent>
{
    private readonly IMediator _mediator;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<TokenResumedEventHandler> _logger;

    public TokenResumedEventHandler(
        IMediator mediator,
        IUnitOfWork uow,
        ILogger<TokenResumedEventHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(TokenResumedEvent notification, CancellationToken ct)
    {
        _logger.LogInformation(
            "[TOKEN-RESUMED] TokenId={TokenId} ProcessId={ProcessId} ElementId={ElementId} IsExecutable={IsExecutable}",
            notification.TokenId,
            notification.ProcessId,
            notification.ElementId,
            notification.IsExecutable);

        // ⛔ NO variable mapping here - only orchestration!

        // Reload token to get current state
        var token = await _uow.Tokens.GetByIdAsync(notification.TokenId, ct);
        if (token is null)
        {
            _logger.LogWarning("[TOKEN-RESUMED] Token not found. TokenId={TokenId}", notification.TokenId);
            return;
        }


        // TOKEN-PROC gate (join/merge/guards)
        var result = await _mediator.Send(new DispatchTokenProcessCommand(token.Id), ct);

        switch (result)
        {
            case TokenProcessResult.Waiting:
            case TokenProcessResult.Consumed:
            case TokenProcessResult.Failed:
            case TokenProcessResult.Terminated:
                _logger.LogInformation(
                    "[TOKEN-RESUMED] TOKEN-PROC returned {Result}. Stop. TokenId={TokenId} ElementId={ElementId}",
                    result, token.Id, token.CurrentElementId);
                return;

            case TokenProcessResult.NoOp:
            case TokenProcessResult.Continue:
                break;

            default:
                _logger.LogDebug(
                    "[TOKEN-RESUMED] TOKEN-PROC returned {Result}. Stop. TokenId={TokenId} ElementId={ElementId}",
                    result, token.Id, token.CurrentElementId);
                return;
        }

        // Reload (TOKEN-PROC may have mutated token state/element in its own tx)
        token = await _uow.Tokens.GetByIdAsync(notification.TokenId, ct);
        if (token is null)
        {
            _logger.LogWarning("[TOKEN-RESUMED] Token not found after TOKEN-PROC. TokenId={TokenId}", notification.TokenId);
            return;
        }

        // If token no longer Active, do not create node
        // (e.g., if join satisfied and token was consumed, or if it became Waiting)
        if (token.State != TokenState.Active && token.State != TokenState.Merged)
        {
            _logger.LogDebug(
                "[TOKEN-MOVED] Skip node creation (TokenState={State}). TokenId={TokenId} ElementId={ElementId}",
                token.State, token.Id, token.CurrentElementId);
            return;
        }

        // Defensive: if somehow became non-executable, NAV only
        if (!token.IsExecutable)
        {
            await _mediator.Send(new DispatchTokenNavigateCommand(token.Id), ct);
            return;
        }

        // Find existing node or create new one
        // Resume semantics: process the node again with isResume=true
        var arrivedViaFlowIds = token.ArrivedViaFlowIds.Count > 0 
            ? token.ArrivedViaFlowIds 
            : null;
        
        var existingNode = await _uow.NodeInstances.TryFindOpenAsync(
            processId: token.ProcessId,
            tokenId: token.Id,
            elementId: token.CurrentElementId,
            scopeId: token.ScopeId,
            activityInstanceId: token.ActivityInstanceId,
            arrivedViaFlowIds: arrivedViaFlowIds,
            cancellationToken: ct);
        
        if (existingNode != null && existingNode.State == NodeState.Waiting)
        {
            // Resume existing node processing
            await _mediator.Send(new DispatchNodeProcessCommand(existingNode.Id, IsResume: true), ct);
        }
        else
        {
            // Create new node instance (shouldn't happen in normal resume flow, but defensive)
            await _mediator.Send(new CreateNodeInstanceCommand(
                ProcessId: token.ProcessId,
                TokenId: token.Id,
                ElementId: token.CurrentElementId,
                IsExecutable: token.IsExecutable,
                ScopeId: token.ScopeId,
                ActivityInstanceId: token.ActivityInstanceId,
                ArrivedViaFlowIds: arrivedViaFlowIds
            ), ct);
        }
    }
}
