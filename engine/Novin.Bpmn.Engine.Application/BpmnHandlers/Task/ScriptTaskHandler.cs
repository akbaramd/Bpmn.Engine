using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

/// <summary>
/// BPMN ScriptTask handler (pure domain).
///
/// Semantics:
/// - Executable token:
///   - Apply input mapping (once)
///   - Execute script
///   - Apply output mapping
///   - token.Processed()
///   - return Completed
///
/// - Trace token:
///   - Skip execution
///   - token.Processed()
///   - return Completed
///
/// - Failure:
///   - token.Fail(...)
///   - return Failed
/// </summary>
public sealed class ScriptTaskHandler : BpmnElementHandlerBase
{
    private readonly IScriptTaskExecutor _executor;
    private readonly IVariableMappingService _variableMapping;

    public ScriptTaskHandler(
        IScriptTaskExecutor executor,
        IVariableMappingService variableMapping,
        IFeelExpressionEvaluator feel,
        ILogger<ScriptTaskHandler> logger)
        : base(feel, logger)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _variableMapping = variableMapping ?? throw new ArgumentNullException(nameof(variableMapping));
    }

    public override bool CanHandle(BpmnFlowElement element)
        => element is BpmnScriptTask;

    public override async Task<ElementProcessResult> NodeProcessAsync(
        Domain.Entities.Process process,
        Token token,
        NodeInstance node,
        BpmnFlowElement element,
        BpmnRuntimeContext ctx,
        bool isResume,
        CancellationToken ct)
    {
        var scriptTask = (BpmnScriptTask)element;

        Logger.LogDebug(
            "[SCRIPT] Start. ProcessId={ProcessId} TokenId={TokenId} NodeId={NodeId} Resume={Resume}",
            process.Id, token.Id, node.Id, isResume);

        // --------------------------------------------------
        // 1) Input mapping (once, first time only)
        // --------------------------------------------------
        if (!isResume)
        {
            token.ClearLocalVariables();
            _variableMapping.ApplyInputs(process, token,node, element, ctx);

            Logger.LogDebug(
                "[SCRIPT] Input mapping applied. TokenId={TokenId}",
                token.Id);
        }

   

        // --------------------------------------------------
        // 3) Execute script
        // --------------------------------------------------
        try
        {
            await _executor.ExecuteAsync(process, token, scriptTask, ct);
        }
        catch (Exception ex)
        {
            node.Fail(ex.Message);

            Logger.LogError(
                ex,
                "[SCRIPT] Script execution threw exception. TokenId={TokenId}",
                token.Id);

            return ElementProcessResult.Failed;
        }

        if (token.State == TokenState.Failed)
        {
            Logger.LogWarning(
                "[SCRIPT] Script execution FAILED. TokenId={TokenId}",
                token.Id);

            return ElementProcessResult.Failed;
        }

        // --------------------------------------------------
        // 4) Output mapping
        // --------------------------------------------------
        _variableMapping.ApplyOutputs(process, token, node,element, ctx);

        Logger.LogDebug(
            "[SCRIPT] Output mapping applied. TokenId={TokenId}",
            token.Id);

        // --------------------------------------------------
        // 5) Done
        // --------------------------------------------------
        token.Processed();
        node.Complete();
        Logger.LogDebug(
            "[SCRIPT] Completed successfully. TokenId={TokenId}",
            token.Id);

        return ElementProcessResult.Completed;
    }

    // TokenNavigateAsync inherited from BpmnElementHandlerBase
    // → pure domain navigation (token.MoveTo / new Token)
}
