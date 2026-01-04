using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Commands.NodeInstances;
using Novin.Bpmn.Engine.Application.Commands.NodeDispatch;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.Events;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

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
            "[TOKEN-MOVED] TokenId={TokenId} ProcessId={ProcessId} From={From} To={To} SkipProcess={Skip}",
            e.TokenId, e.ProcessId, e.FromElementId, e.ToElementId, e.SkipProcess);

        // 1) TOKEN-PROC first (so join gateways can return Waiting and block node creation)
        var tokenProcResult = TokenProcessResult.Continue;

        if (!e.SkipProcess)
        {
            tokenProcResult = await _mediator.Send(new DispatchTokenProcessCommand(e.TokenId), ct);

            switch (tokenProcResult)
            {
                case TokenProcessResult.Waiting:
                    // join gateway waits => no node instance
                    _logger.LogInformation(
                        "[TOKEN-MOVED] TOKEN-PROC=Waiting => NO NodeInstance. TokenId={TokenId} ElementId={El}",
                        e.TokenId, e.ToElementId);
                    return;

                case TokenProcessResult.Consumed:
                case TokenProcessResult.Failed:
                case TokenProcessResult.Terminated:
                    _logger.LogInformation(
                        "[TOKEN-MOVED] TOKEN-PROC={Result} => stop. TokenId={TokenId} ElementId={El}",
                        tokenProcResult, e.TokenId, e.ToElementId);
                    return;

                case TokenProcessResult.NoOp:
                case TokenProcessResult.Continue:
                    break;

                default:
                    _logger.LogDebug(
                        "[TOKEN-MOVED] TOKEN-PROC={Result} => stop. TokenId={TokenId} ElementId={El}",
                        tokenProcResult, e.TokenId, e.ToElementId);
                    return;
            }
        }

        // 2) Reload token (TOKEN-PROC may have changed element/state)
        var token = await _uow.Tokens.GetByIdAsync(e.TokenId, ct);
        if (token is null)
        {
            _logger.LogWarning("[TOKEN-MOVED] Token not found after TOKEN-PROC. TokenId={TokenId}", e.TokenId);
            return;
        }

        // 3) If TOKEN-PROC moved token again (pass-through routing), do NOT create node for old element
        if (!string.Equals(token.CurrentElementId, e.ToElementId, StringComparison.Ordinal))
        {
            _logger.LogDebug(
                "[TOKEN-MOVED] Token moved again during TOKEN-PROC => skip node creation for old element. TokenId={TokenId} Old={Old} Now={Now}",
                token.Id, e.ToElementId, token.CurrentElementId);
            return;
        }

        // ✅ 4) IMPORTANT: create node even if token became Completed/Terminated at EndEvent
        // Decision is based on tokenProcResult (Continue/NoOp), not token.State.
        var arrivedViaFlowIds = token.ArrivedViaFlowIds.Count > 0 ? token.ArrivedViaFlowIds : null;

        await _mediator.Send(new CreateNodeInstanceCommand(
            ProcessId: token.ProcessId,
            TokenId: token.Id,
            ElementId: token.CurrentElementId,
            ScopeId: token.ScopeId,
            ActivityInstanceId: token.ActivityInstanceId,
            ArrivedViaFlowIds: arrivedViaFlowIds
        ), ct);

        _logger.LogDebug(
            "[TOKEN-MOVED] NodeInstance created. TokenId={TokenId} ElementId={El} TokenState={State} TokenProc={ProcResult}",
            token.Id, token.CurrentElementId, token.State, tokenProcResult);
    }
}
