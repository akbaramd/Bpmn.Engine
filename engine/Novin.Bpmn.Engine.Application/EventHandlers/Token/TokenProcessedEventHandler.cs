using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Events;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

/// <summary>
/// Handles token terminal events (Completed/Terminated/Failed) and evaluates process completion.
/// This follows BPMN2 semantics: process completes when no live executable tokens remain.
/// After token completion, dispatches navigation to move to the next element.
/// Since completed tokens cannot be moved, navigation creates new tokens at target elements.
/// </summary>
public sealed class TokenProcessedEventHandler : INotificationHandler<TokenProcessedEvent>
{
    private readonly IProcessCompletionEvaluator _evaluator;
    private readonly IUnitOfWork _uow;
    private readonly IBpmnRuntimeContextFactory _ctxFactory;
    private readonly ITokenExecutionDispatcher _dispatcher;
    private readonly IMediator _mediator;
    private readonly IProcessExecutionRecorder _executionRecorder;
    private readonly ILogger<TokenProcessedEventHandler> _logger;

    public TokenProcessedEventHandler(
        IProcessCompletionEvaluator evaluator,
        IUnitOfWork uow,
        IBpmnRuntimeContextFactory ctxFactory,
        ITokenExecutionDispatcher dispatcher,
        IMediator mediator,
        IProcessExecutionRecorder executionRecorder,
        ILogger<TokenProcessedEventHandler> logger)
    {
        _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _ctxFactory = ctxFactory ?? throw new ArgumentNullException(nameof(ctxFactory));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _executionRecorder = executionRecorder ?? throw new ArgumentNullException(nameof(executionRecorder));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async System.Threading.Tasks.Task Handle(TokenProcessedEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[TOKEN-ENDED] ✅ Token completed event received. TokenId={TokenId} ProcessId={ProcessId} ElementId={ElementId} IsExecutable={Executable} ScopeId={ScopeId} OccurredAt={OccurredAt}",
            notification.TokenId,
            notification.ProcessId,
            notification.ElementId,
            notification.IsExecutable,
            notification.ScopeId,
            notification.OccurredAtUtc);

        _logger.LogDebug(
            "[TOKEN-ENDED] Evaluating process completion. ProcessId={ProcessId}",
            notification.ProcessId);

        // Evaluate process completion first
        await _evaluator.EvaluateCompletionAsync(notification.ProcessId, cancellationToken);

        // Store element info for recording (before transaction)
        string? completedElementId = null;
        string? arrivedViaFlowId = null;

        // Dispatch navigation to move to next element
        await _uow.ExecuteInTransactionAsync(async trxCt =>
        {
            // Load process and token
            var process = await _uow.Processes.GetByIdAsync(notification.ProcessId, trxCt);
            if (process == null)
            {
                _logger.LogWarning("[TOKEN-ENDED] Process not found. ProcessId={ProcessId}", notification.ProcessId);
                return;
            }

            var token = await _uow.Tokens.GetByIdAsync(notification.TokenId, trxCt);
            if (token == null || token.ProcessId != notification.ProcessId)
            {
                _logger.LogWarning("[TOKEN-ENDED] Token not found or process mismatch. TokenId={TokenId} ProcessId={ProcessId}",
                    notification.TokenId, notification.ProcessId);
                return;
            }

            // Store element info for recording after transaction
            completedElementId = token.CurrentElementId;
            arrivedViaFlowId = token.ArrivedViaFlowId;

            // Build runtime context
            var ctx = await _ctxFactory.CreateAsync(process, trxCt);

            // Get the current element (where token completed)
            var element = ctx.Model.GetElementById(ctx.BpmnProcessId, token.CurrentElementId);
            if (element == null)
            {
                _logger.LogWarning(
                    "[TOKEN-ENDED] Element not found in BPMN model. ProcessId={ProcessId} TokenId={TokenId} ElementId={ElementId}",
                    notification.ProcessId, notification.TokenId, token.CurrentElementId);
                return;
            }

            // Dispatch navigation using the dispatcher
            // The dispatcher will call NavigateAsync on the appropriate handler
            // NavigateAsync should handle the case where token is completed
            _logger.LogDebug(
                "[TOKEN-ENDED] Dispatching navigation. ProcessId={ProcessId} TokenId={TokenId} ElementId={ElementId} TokenState={TokenState}",
                notification.ProcessId, notification.TokenId, token.CurrentElementId, token.State);

            await _dispatcher.DispatchNavigateAsync(process, token, element, ctx, isResume: false, trxCt);
        }, cancellationToken);

        // ---- Record node execution (best-effort, after commit) ----
        if (!string.IsNullOrWhiteSpace(completedElementId))
        {
            try
            {
                var process = await _uow.Processes.GetByIdAsync(notification.ProcessId, cancellationToken);
                if (process == null)
                    return;

                var token = await _uow.Tokens.GetByIdAsync(notification.TokenId, cancellationToken);
                if (token == null)
                    return;

                // Record node execution for the completed element
                await _executionRecorder.RecordNodeExecutionAsync(
                    process: process,
                    token: token,
                    nodeId: completedElementId,
                    arrivedViaFlowId: arrivedViaFlowId,
                    ct: cancellationToken);

                _logger.LogDebug(
                    "[TOKEN-ENDED] Recorded node execution. ProcessId={ProcessId} TokenId={TokenId} ElementId={ElementId}",
                    notification.ProcessId, notification.TokenId, completedElementId);
            }
            catch (Exception ex)
            {
                // recorder shouldn't fail the engine
                _logger.LogWarning(ex,
                    "[TOKEN-ENDED] Node execution recording failed (best-effort). ProcessId={ProcessId} TokenId={TokenId} ElementId={ElementId}",
                    notification.ProcessId, notification.TokenId, completedElementId);
            }
        }

        _logger.LogTrace(
            "[TOKEN-ENDED] Completion evaluation and navigation finished. ProcessId={ProcessId} TokenId={TokenId}",
            notification.ProcessId,
            notification.TokenId);
    }
}