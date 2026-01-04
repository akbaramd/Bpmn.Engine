using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Models.Models;
using NodeState = Novin.Bpmn.Engine.Domain.Entities.NodeState;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

/// <summary>
/// BPMN ScriptTask handler (job-inside, no mediator).
///
/// Semantics (production-ready):
/// - NonExecutable/Trace:
///     - Do NOT execute script
///     - token.Processed(); node.Complete(); return Completed
/// - First run:
///     - Apply inputs once
///     - Execute script
///     - Apply outputs
///     - token.Processed(); node.Complete(); return Completed
/// - Resume:
///     - Re-check terminal guards
///     - Execute script again ONLY if you call this handler on resume (kept same semantics as your code)
/// - Errors:
///     - Classify failures into EngineErrorKind:
///         - Logical     => validation/precondition errors (bad model, missing script, etc.)
///         - BpmnError   => BPMN error semantics (catchable by boundary error via ErrorCode)
///         - Technical   => infra/runtime exceptions/timeouts/crashes
///     - Only node is failed here (NO token.Fail)
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
        if (process is null) throw new ArgumentNullException(nameof(process));
        if (token is null) throw new ArgumentNullException(nameof(token));
        if (node is null) throw new ArgumentNullException(nameof(node));
        if (element is null) throw new ArgumentNullException(nameof(element));
        if (ctx is null) throw new ArgumentNullException(nameof(ctx));

        var scriptTask = (BpmnScriptTask)element;

        Logger.LogDebug(
            "[SCRIPT] Start. P={ProcessId} T={TokenId} N={NodeId} Resume={Resume} TokenState={TokenState} NodeState={NodeState}",
            process.Id, token.Id, node.Id, isResume, token.State, node.State);

        // -----------------------------
        // 0) Terminal / immutability guards
        // -----------------------------
        if (token.State is TokenState.Terminated or TokenState.Failed)
        {
            // Do NOT mutate token here. Mirror into node defensively.
            if (token.State == TokenState.Failed && node.State != NodeState.Failed)
                node.Fail("Token is already Failed (terminal).", EngineErrorKind.Logical);

            if (token.State == TokenState.Terminated && node.State != NodeState.Completed)
                node.Complete();

            return ElementProcessResult.NoOp;
        }

        if (node.State is NodeState.Completed or NodeState.Failed or NodeState.Skipped)
            return ElementProcessResult.NoOp;

        // -----------------------------
        // 1) Trace / non-executable semantics
        // -----------------------------
        // Your domain comment says: Trace token => skip execution but Processed+Complete.
        // Here we implement it using node.IsExecutable (your NodeInstance has IsExecutable).
        if (!node.IsExecutable)
        {
            token.Processed();
            node.Complete();

            Logger.LogDebug("[SCRIPT] NonExecutable => skipped script. T={TokenId} N={NodeId}", token.Id, node.Id);
            return ElementProcessResult.Completed;
        }

        // -----------------------------
        // 2) Input mapping (once, first time only)
        // -----------------------------
        if (!isResume)
        {
            token.ClearLocalVariables();
            _variableMapping.ApplyInputs(process, token, node, element, ctx);

            Logger.LogDebug("[SCRIPT] Input mapping applied. T={TokenId} N={NodeId}", token.Id, node.Id);
        }

        // Optional: ensure node state progression
        if (node.State == NodeState.Created)
            node.Start();

        // -----------------------------
        // 3) Validate model / script presence (Logical)
        // -----------------------------
        // (We avoid depending on concrete model fields; keep conservative checks.)
        if (string.IsNullOrWhiteSpace(scriptTask.id) && string.IsNullOrWhiteSpace(node.ElementId))
        {
            node.Fail("ScriptTask element id is missing.", EngineErrorKind.Logical);
            return ElementProcessResult.Failed;
        }

        // -----------------------------
        // 4) Execute script + classify errors
        // -----------------------------
        try
        {
            await _executor.ExecuteAsync(process, token, scriptTask, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Let pipeline stop cleanly; do not mark failed.
            throw;
        }
        catch (Exception ex)
        {
            var (kind, message) = ClassifyException(ex);

            // If it's a BPMN error, we want errorCode semantics available to boundary error.
            // Your NodeInstance.Fail signature: Fail(string errorMessage, string errorCode)
            // You said you changed it to: Fail(string errorMessage, EngineErrorKind errorKind)
            // So we call that new signature here.
            node.Fail(message, kind);

            Logger.LogError(ex,
                "[SCRIPT] Execution failed. Kind={Kind} P={P} T={T} N={N} E={E}",
                kind, process.Id, token.Id, node.Id, node.ElementId);

            return ElementProcessResult.Failed;
        }

        // -----------------------------
        // 5) Output mapping (can also throw => Technical)
        // -----------------------------
        try
        {
            _variableMapping.ApplyOutputs(process, token, node, element, ctx);

            Logger.LogDebug("[SCRIPT] Output mapping applied. T={TokenId} N={NodeId}", token.Id, node.Id);
        }
        catch (Exception ex)
        {
            node.Fail("Output mapping failed.", EngineErrorKind.Technical);

            Logger.LogError(ex,
                "[SCRIPT] Output mapping threw exception. P={P} T={T} N={N}",
                process.Id, token.Id, node.Id);

            return ElementProcessResult.Failed;
        }

        // -----------------------------
        // 6) Done
        // -----------------------------
        token.Processed();
        node.Complete();

        Logger.LogDebug("[SCRIPT] Completed successfully. T={TokenId} N={NodeId}", token.Id, node.Id);
        return ElementProcessResult.Completed;
    }

    /// <summary>
    /// Maps thrown exceptions to EngineErrorKind.
    /// This keeps the handler consistent and allows boundary error logic to behave correctly.
    ///
    /// Heuristics (safe defaults):
    /// - ArgumentException / InvalidOperationException / FormatException => Logical
    /// - Exceptions that look like "BPMN Error" (custom exception types or markers) => BpmnError
    /// - Everything else => Technical
    ///
    /// If you later introduce a concrete BpmnErrorException (recommended),
    /// add a direct check here to return (BpmnError, ex.Message) and extract ErrorCode.
    /// </summary>
    private static (EngineErrorKind Kind, string Message) ClassifyException(Exception ex)
    {
        // If you add a dedicated exception, plug it here:
        // if (ex is BpmnErrorException bex) return (EngineErrorKind.BpmnError, bex.Message);

        if (ex is ArgumentException or InvalidOperationException or FormatException)
            return (EngineErrorKind.Logical, ex.Message);

        // Common “business rule” style exceptions sometimes use these names
        var typeName = ex.GetType().Name;
        if (typeName.Contains("Validation", StringComparison.OrdinalIgnoreCase) ||
            typeName.Contains("Business", StringComparison.OrdinalIgnoreCase) ||
            typeName.Contains("Rule", StringComparison.OrdinalIgnoreCase))
        {
            return (EngineErrorKind.Logical, ex.Message);
        }

        // Soft heuristic for BPMN error semantics if you don't yet have a dedicated exception type:
        // (Prefer introducing BpmnErrorException instead of relying on messages.)
        if (typeName.Contains("BpmnError", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("BPMN_ERROR", StringComparison.OrdinalIgnoreCase))
        {
            return (EngineErrorKind.BpmnError, ex.Message);
        }

        return (EngineErrorKind.Technical, ex.Message);
    }
}
