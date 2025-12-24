using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

/// <summary>
/// Handles BPMN End Events according to BPMN2 semantics:
/// - Normal End Event: completes the token
/// - Terminate End Event: terminates all live tokens and the process
/// </summary>
public sealed class EndEventHandler : IBpmnElementHandler
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<EndEventHandler> _logger;

    public EndEventHandler(
        IUnitOfWork uow,
        ILogger<EndEventHandler> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool CanHandle(BpmnFlowElement element) => element is BpmnEndEvent;

    public async Task HandleAsync(Process process, Token token, BpmnFlowElement element, BpmnRuntimeContext ctx, CancellationToken ct)
    {
        var endEvent = (BpmnEndEvent)element;
        var isTerminateEndEvent = IsTerminateEndEvent(endEvent);

        _logger.LogInformation(
            "[END-EVENT] End Event reached. ProcessId={ProcessId} TokenId={TokenId} ElementId={ElementId} IsTerminate={IsTerminate} TokenState={TokenState} IsExecutable={Executable}",
            process.Id,
            token.Id,
            element.id,
            isTerminateEndEvent,
            token.State,
            token.IsExecutable);

        if (isTerminateEndEvent)
        {
            _logger.LogWarning(
                "[END-EVENT] ⚠️ TERMINATE End Event detected. Will terminate all tokens and process. ProcessId={ProcessId} TokenId={TokenId} ElementId={ElementId}",
                process.Id,
                token.Id,
                element.id);

            // 1. Complete the current token
            var currentTokenAction = token.IsExecutable ? "Complete" : "Terminate";
            _logger.LogDebug(
                "[END-EVENT] Step 1: {Action} current token. ProcessId={ProcessId} TokenId={TokenId}",
                currentTokenAction,
                process.Id,
                token.Id);

            if (token.IsExecutable)
                token.Complete();
            else
                token.Terminate("Terminate End Event reached");

            process.RemoveToken(token.Id);

            _logger.LogDebug(
                "[END-EVENT] Step 1 done. Current token {Action}. ProcessId={ProcessId} TokenId={TokenId}",
                currentTokenAction,
                process.Id,
                token.Id);

            // 2. Terminate all other live tokens
            _logger.LogDebug(
                "[END-EVENT] Step 2: Loading all tokens to find live ones. ProcessId={ProcessId}",
                process.Id);

            var allTokens = await _uow.Tokens.GetByProcessIdAsync(process.Id, ct);
            var tokensList = allTokens.ToList();

            _logger.LogDebug(
                "[END-EVENT] Step 2: Tokens loaded. ProcessId={ProcessId} TotalTokens={Total}",
                process.Id,
                tokensList.Count);

            var otherLiveTokens = tokensList
                .Where(t => t.Id != token.Id && IsLiveToken(t))
                .ToList();

            var liveTokenDetails = string.Join(", ", otherLiveTokens.Select(t => $"{t.Id}({t.State})").ToList());
            _logger.LogInformation(
                "[END-EVENT] Step 2: Found {Count} other live tokens to terminate. ProcessId={ProcessId} LiveTokenIds={TokenIds}",
                otherLiveTokens.Count,
                process.Id,
                liveTokenDetails);

            foreach (var liveToken in otherLiveTokens)
            {
                _logger.LogDebug(
                    "[END-EVENT] Step 2: Terminating live token. ProcessId={ProcessId} TokenId={TokenId} State={State} ElementId={ElementId}",
                    process.Id,
                    liveToken.Id,
                    liveToken.State,
                    liveToken.CurrentElementId);

                liveToken.Terminate("Terminate End Event reached");
                process.RemoveToken(liveToken.Id);

                _logger.LogDebug(
                    "[END-EVENT] Step 2: Token terminated and removed. ProcessId={ProcessId} TokenId={TokenId}",
                    process.Id,
                    liveToken.Id);
            }

            // 3. Terminate the process
            _logger.LogWarning(
                "[END-EVENT] Step 3: Terminating process. ProcessId={ProcessId}",
                process.Id);

            process.Terminate("Terminate End Event reached");

            _logger.LogWarning(
                "[END-EVENT] ✅ Process terminated successfully. ProcessId={ProcessId} TerminatedTokens={Count}",
                process.Id,
                otherLiveTokens.Count);
        }
        else
        {
            // Normal End Event: just complete/terminate this token
            _logger.LogInformation(
                "[END-EVENT] Normal End Event. Completing/terminating current token only. ProcessId={ProcessId} TokenId={TokenId} ElementId={ElementId} TokenState={TokenState} IsExecutable={Executable}",
                process.Id,
                token.Id,
                element.id,
                token.State,
                token.IsExecutable);

            var action = token.IsExecutable ? "Complete" : "Terminate";
            _logger.LogDebug(
                "[END-EVENT] {Action} token. ProcessId={ProcessId} TokenId={TokenId}",
                action,
                process.Id,
                token.Id);

            if (token.IsExecutable)
                token.Complete();
            else
                token.Terminate();

            process.RemoveToken(token.Id);

            _logger.LogInformation(
                "[END-EVENT] ✅ Token {Action} and removed. ProcessId={ProcessId} TokenId={TokenId} RemainingTokens={Remaining}",
                action,
                process.Id,
                token.Id,
                process.TokenIds.Count);
        }
    }

    /// <summary>
    /// Checks if an End Event is a Terminate End Event by looking for terminateEventDefinition.
    /// </summary>
    private static bool IsTerminateEndEvent(BpmnEndEvent endEvent)
    {
        // BpmnThrowEvent has Items property (array of event definitions)
        // Check if any eventDefinition is BpmnTerminateEventDefinition
        var eventDefinitions = endEvent.Items;
        if (eventDefinitions == null || eventDefinitions.Length == 0)
            return false;

        return eventDefinitions.Any(ed => ed is BpmnTerminateEventDefinition);
    }

    /// <summary>
    /// Determines if a token is "live" (should be terminated by Terminate End Event).
    /// </summary>
    private static bool IsLiveToken(Token token)
    {
        if (!token.IsExecutable)
            return false;

        return token.State is TokenState.Created
            or TokenState.Active
            or TokenState.Waiting;
    }
}