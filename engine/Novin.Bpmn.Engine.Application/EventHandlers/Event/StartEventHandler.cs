using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

/// <summary>
/// Production-ready StartEvent handler (token-centric).
///
/// Semantics (matches your new pipeline):
/// - StartEvent does NOT do work; it just "finishes" immediately.
/// - We DO NOT move inside the handler.
/// - We COMPLETE the token; TokenCompletedEventHandler will call NavigateAsync (or your orchestrator will)
///   and the token will be moved using MoveTokenCommand.
///
/// Notes:
/// - Input/Output variable mapping is intentionally not done here (usually none for StartEvent).
/// - Trace tokens are supported: they still complete and will navigate as trace.
/// - Supports multiple outgoing flows:
///     - If conditional flows exist, it evaluates FEEL and chooses first TRUE (model order)
///     - Else tries default (if any; uncommon for StartEvent)
///     - Else falls back to unconditional/first
/// </summary>
public sealed class StartEventHandler : BpmnElementHandlerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<StartEventHandler> _logger;

    public StartEventHandler(
        IMediator mediator,
        IFeelExpressionEvaluator feel,
        ILogger<StartEventHandler> logger,
        bool includeProcessVars = false)
        : base(mediator, feel, logger, includeProcessVars)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public override bool CanHandle(BpmnFlowElement element) => element is BpmnStartEvent;

    public override async Task<ElementProcessResult> ProcessAsync(
        Domain.Entities.Process process,
        Token token,
        BpmnFlowElement element,
        BpmnRuntimeContext ctx,
        bool isResume,
        CancellationToken ct)
    {
        if (process is null) throw new ArgumentNullException(nameof(process));
        if (token is null) throw new ArgumentNullException(nameof(token));
        if (element is null) throw new ArgumentNullException(nameof(element));
        if (ctx is null) throw new ArgumentNullException(nameof(ctx));

        using (_logger.BeginScope(new Dictionary<string, string?>
               {
                   ["ProcessId"] = process.Id.ToString(),
                   ["TokenId"] = token.Id.ToString(),
                   ["ElementId"] = token.CurrentElementId,
                   ["ElementType"] = element.GetType().Name,
                   ["Executable"] = token.IsExecutable.ToString(),
                   ["ScopeId"] = token.ScopeId?.ToString(),
                   ["ArrivedVia"] = token.ArrivedViaFlowId
               }))
        {
            _logger.LogInformation(
                "[START] ProcessAsync. TokenState={State} Exec={Exec} Resume={Resume}",
                token.State, token.IsExecutable, isResume);

            // StartEvent should be processed only when token is Active (defensive)
            if (token.State != TokenState.Active)
            {
                _logger.LogWarning("[START] Ignored. Token state={State} (expected Active).", token.State);
                return ElementProcessResult.NoOp;
            }

            // If process model is broken (no outgoing), we still complete the token so engine can evaluate completion.
            var outgoing = ctx.Model.GetOutgoingSequenceFlows(ctx.BpmnProcessId, token.CurrentElementId);
            if (outgoing is null || outgoing.Count == 0)
            {
                _logger.LogWarning("[START] No outgoing flow from StartEvent. Completing token anyway.");
            }

            // ✅ IMPORTANT: Mark token as processed (NodeDone)
            // TokenProcessedEvent will trigger navigation
            token.Processed();

            _logger.LogInformation("[START] Completed start token. TokenId={TokenId}", token.Id);

            // Tell orchestrator that after completion it should navigate
            return ElementProcessResult.Completed;
        }
    }

    /// <summary>
    /// Navigate after completion (called by your TokenCompleted pipeline).
    /// Uses base navigation rules (FEEL evaluation, default/unconditional fallback).
    /// </summary>
    public override System.Threading.Tasks.Task NavigateAsync(
        Domain.Entities.Process process,
        Token token,
        BpmnFlowElement element,
        BpmnRuntimeContext ctx,
        bool isResume,
        CancellationToken ct)
        => base.NavigateAsync(process, token, element, ctx, isResume, ct);
}

