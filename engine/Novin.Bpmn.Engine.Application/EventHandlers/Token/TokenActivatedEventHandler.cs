// Application/Events/TokenActivatedEventHandler.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Commands.NodeInstances;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Events;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

/// <summary>
/// When a Token becomes Active, engine must start processing its CurrentElement.
/// This handler orchestrates:
/// 1) Create NodeInstance (only if token.IsExecutable)
/// 2) Dispatch node processing (dispatcher.Process)
/// </summary>
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

    public async Task Handle(TokenActivatedEvent notification, CancellationToken cancellationToken)
    {
        // Outbox guarantees we run after token is persisted.
        var token = await _uow.Tokens.GetByIdAsync(notification.TokenId, cancellationToken);
        if (token is null)
        {
            _logger.LogWarning("TokenActivatedEvent: token not found. TokenId={TokenId}", notification.TokenId);
            return;
        }

        // Non-executable tokens are trace/bypass only; they should be navigated (no NodeInstance).
        if (!token.IsExecutable)
        {
            _logger.LogInformation(
                "TokenActivatedEvent: token is non-executable. TokenId={TokenId}, ElementId={ElementId}. Skipping NodeInstance creation.",
                token.Id, token.CurrentElementId);

            // If you have a Navigate pipeline, trigger it here (not requested in this task).
            // await _mediator.Send(new DispatchTokenNavigateCommand(token.ProcessId, token.Id), cancellationToken);
            return;
        }

        // Create NodeInstance (command)
        var nodeId = await _mediator.Send(new CreateNodeInstanceCommand(
            ProcessId: token.ProcessId,
            TokenId: token.Id,
            ElementId: token.CurrentElementId,
            ScopeId: token.ScopeId,
            ActivityInstanceId: token.ActivityInstanceId,
            ArrivedViaFlowId: token.ArrivedViaFlowId
        ), cancellationToken);

   
    }
}
