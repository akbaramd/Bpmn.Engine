// Application/EventHandlers/TokenActivatedEventHandler.cs
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

public sealed class TokenActivatedEventHandler : INotificationHandler<TokenActivatedEvent>
{
    private readonly IUnitOfWork _uow;
    private readonly IMediator _mediator;
    private readonly ILogger<TokenActivatedEventHandler> _logger;

    public TokenActivatedEventHandler(
        IUnitOfWork uow,
        IMediator mediator,
        ILogger<TokenActivatedEventHandler> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(TokenActivatedEvent notification, CancellationToken ct)
    {
        var token = await _uow.Tokens.GetByIdAsync(notification.TokenId, ct);
        if (token is null)
        {
            _logger.LogWarning("[TOKEN-ACTIVATED] Token not found. TokenId={TokenId}", notification.TokenId);
            return;
        }

        // Non-executable => NAV only (no node)
        if (!token.IsExecutable)
        {
            _logger.LogInformation(
                "[TOKEN-ACTIVATED] Non-executable token. NAV only. TokenId={TokenId} ElementId={ElementId}",
                token.Id, token.CurrentElementId);

            await _mediator.Send(new DispatchTokenNavigateCommand(token.Id), ct);
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
                    "[TOKEN-ACTIVATED] TOKEN-PROC returned {Result}. Stop. TokenId={TokenId} ElementId={ElementId}",
                    result, token.Id, token.CurrentElementId);
                return;

            case TokenProcessResult.NoOp:
            case TokenProcessResult.Continue:
                break;

            default:
                _logger.LogDebug(
                    "[TOKEN-ACTIVATED] TOKEN-PROC returned {Result}. Stop. TokenId={TokenId} ElementId={ElementId}",
                    result, token.Id, token.CurrentElementId);
                return;
        }

        // Reload (TOKEN-PROC may have mutated token state/element in its own tx)
        token = await _uow.Tokens.GetByIdAsync(notification.TokenId, ct);
        if (token is null)
        {
            _logger.LogWarning("[TOKEN-ACTIVATED] Token not found after TOKEN-PROC. TokenId={TokenId}", notification.TokenId);
            return;
        }

        // If token no longer Active, do not create node
        if (token.State != TokenState.Active)
        {
            _logger.LogDebug(
                "[TOKEN-ACTIVATED] Skip node creation (TokenState={State}). TokenId={TokenId} ElementId={ElementId}",
                token.State, token.Id, token.CurrentElementId);
            return;
        }

        // Defensive: if somehow became non-executable, NAV only
        if (!token.IsExecutable)
        {
            await _mediator.Send(new DispatchTokenNavigateCommand(token.Id), ct);
            return;
        }

        // Create NodeInstance (NodeCreatedDomainEventHandler enqueues DispatchNodeProcessCommand)
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
