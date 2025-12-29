using Microsoft.Extensions.Logging;
using MediatR;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

/// <summary>
/// BPMN ScriptTask handler (Process / Navigate separated).
///
/// Semantics:
/// - Executable token:
///   - Apply input mapping
///   - Execute script
///   - Apply output mapping
///   - Return Completed → dispatcher will call NavigateAsync
///
/// - Trace token:
///   - Skip execution
///   - Return Completed → dispatcher will call NavigateAsync
///
/// - Failed token:
///   - Return Failed → no navigation
/// </summary>
public sealed class ScriptTaskHandler : BpmnElementHandlerBase
{
    private readonly IScriptTaskExecutor _executor;
    private readonly IVariableMappingService _variableMapping;

    public ScriptTaskHandler(
        IScriptTaskExecutor executor,
        IVariableMappingService variableMapping,
        IMediator mediator,
        IFeelExpressionEvaluator feel,
        ILogger<ScriptTaskHandler> logger)
        : base(mediator, feel, logger)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _variableMapping = variableMapping ?? throw new ArgumentNullException(nameof(variableMapping));
    }

    public override bool CanHandle(BpmnFlowElement element)
        => element is BpmnScriptTask;

    public override async Task<ElementProcessResult> ProcessAsync(
        Domain.Entities.Process process,
        Token token,
        BpmnFlowElement element,
        BpmnRuntimeContext ctx,
        bool isResume,
        CancellationToken ct)
    {
        var scriptTask = (BpmnScriptTask)element;

        Logger.LogDebug(
            "[SCRIPT] Enter ProcessAsync. TokenId={TokenId} Exec={Exec} Resume={Resume}",
            token.Id, token.IsExecutable, isResume);

        // --------------------------------------------------
        // 1) Input mapping (only once, only executable)
        // --------------------------------------------------
        if (token.IsExecutable && !isResume)
        {
            token.ClearLocalVariables();
            _variableMapping.ApplyInputs(process, token, element, ctx);

            Logger.LogDebug(
                "[SCRIPT] Input mapping applied. TokenId={TokenId}",
                token.Id);
        }

        // --------------------------------------------------
        // 2) Trace token → skip execution
        // --------------------------------------------------
        if (!token.IsExecutable)
        {
            Logger.LogDebug(
                "[SCRIPT] Trace token → skipping execution. TokenId={TokenId}",
                token.Id);

            // ✅ Mark token as processed (NodeDone) even for trace tokens
            token.Processed();

            return ElementProcessResult.Completed;
        }

        // --------------------------------------------------
        // 3) Execute script
        // --------------------------------------------------
        await _executor.ExecuteAsync(process, token, scriptTask, ct);

        if (token.State == TokenState.Failed)
        {
            Logger.LogWarning(
                "[SCRIPT] Script execution FAILED. TokenId={TokenId}",
                token.Id);

            return ElementProcessResult.Failed;
        }

        // --------------------------------------------------
        // 4) Output mapping (only executable)
        // --------------------------------------------------
        _variableMapping.ApplyOutputs(process, token, element, ctx);

        Logger.LogDebug(
            "[SCRIPT] Output mapping applied. TokenId={TokenId}",
            token.Id);

        // --------------------------------------------------
        // 5) ScriptTask finished successfully → mark token as processed
        // --------------------------------------------------
        // ✅ IMPORTANT: Mark token as processed (NodeDone)
        token.Processed();

        Logger.LogDebug(
            "[SCRIPT] Token completed. TokenId={TokenId}",
            token.Id);

        return ElementProcessResult.Completed;
    }

    // NavigateAsync inherited from BpmnElementHandlerBase
    // → default routing + MoveTokenCommand
}
