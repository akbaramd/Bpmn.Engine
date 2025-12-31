using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Commands.NodeDispatch;
using Novin.Bpmn.Engine.Application.Commands.NodeInstances;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services; // TokenProcessResult
using Novin.Bpmn.Engine.Domain.Entities;      // TokenState
using Novin.Bpmn.Engine.Domain.Events;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

/// <summary>
/// Handles TokenMovedEvent when token moves to a new element.
/// 
/// ⛔ IMPORTANT: Join gateway is token-driven, not node-driven!
/// If TokenProcess returns Waiting/Consumed/Failed/Terminated, NO NodeInstance is created.
/// </summary>
public sealed class TokenMovedEventHandler : INotificationHandler<TokenMovedEvent>
{
    private readonly IMediator _mediator;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<TokenMovedEventHandler> _logger;

    public TokenMovedEventHandler(
        IMediator mediator,
        IUnitOfWork uow,
        ILogger<TokenMovedEventHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(TokenMovedEvent e, CancellationToken ct)
    {
        _logger.LogInformation(
            "[TOKEN-MOVED] TokenId={TokenId} ProcessId={ProcessId} From={From} To={To} Executable={Executable}",
            e.TokenId, e.ProcessId, e.FromElementId, e.ToElementId, e.IsExecutable);

        // Non-executable => NAV only (no node)
        if (!e.IsExecutable)
        {
            _logger.LogDebug(
                "[TOKEN-MOVED] Non-executable token. NAV only. TokenId={TokenId} To={To}",
                e.TokenId, e.ToElementId);

            await _mediator.Send(new DispatchTokenNavigateCommand(e.TokenId), ct);
            return;
        }

        // TOKEN-PROC gate (join/merge/guards) BEFORE creating node
        // ⚠️ Join gateway: if result is Waiting, token will be Waiting and NO NodeInstance is created
        var result = await _mediator.Send(new DispatchTokenProcessCommand(e.TokenId), ct);

        switch (result)
        {
            case TokenProcessResult.Waiting:
                // ✅ Join waiting: token is Waiting, NO NodeInstance created (token-driven join)
                _logger.LogInformation(
                    "[TOKEN-MOVED] TOKEN-PROC returned Waiting (join gateway). TokenId={TokenId} ElementId={ElementId} - NO NodeInstance created",
                    e.TokenId, e.ToElementId);
                return;

            case TokenProcessResult.Consumed:
            case TokenProcessResult.Failed:
            case TokenProcessResult.Terminated:
                _logger.LogInformation(
                    "[TOKEN-MOVED] TOKEN-PROC returned {Result}. Stop. TokenId={TokenId} ElementId={ElementId}",
                    result, e.TokenId, e.ToElementId);
                return;

            case TokenProcessResult.NoOp:
            case TokenProcessResult.Continue:
                break;

            default:
                _logger.LogDebug(
                    "[TOKEN-MOVED] TOKEN-PROC returned {Result}. Stop. TokenId={TokenId} ElementId={ElementId}",
                    result, e.TokenId, e.ToElementId);
                return;
        }

        // Reload token (TOKEN-PROC may have mutated token state/element in its own tx)
        var token = await _uow.Tokens.GetByIdAsync(e.TokenId, ct);
        if (token is null)
        {
            _logger.LogWarning("[TOKEN-MOVED] Token not found after TOKEN-PROC. TokenId={TokenId}", e.TokenId);
            return;
        }

        // If token no longer Active, do not create node
        // (e.g., if join satisfied and token was consumed, or if it became Waiting)
        if (token.State != TokenState.Active)
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

        // ✅ Only create NodeInstance if token is Active and Continue was returned
        // Create node for the new element (NodeCreatedDomainEventHandler enqueues node processing)
        // Convert Token's single ArrivedViaFlowId to array
        var arrivedViaFlowIds = string.IsNullOrWhiteSpace(token.ArrivedViaFlowId)
            ? null
            : new[] { token.ArrivedViaFlowId };
        
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
