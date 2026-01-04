using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Models.Models;
using NodeState = Novin.Bpmn.Engine.Domain.Entities.NodeState;

namespace Novin.Bpmn.Engine.Application.ElementHandlers;

/// <summary>
/// BPMN UserTask handler (production-ready).
///
/// Responsibilities:
/// - NonExecutable/Trace: DO NOT create user task; Complete node; Token.Processed()
/// - First run:
///     - Apply inputs once
///     - CreateOrGetAsync idempotently
///     - Put Token+Node into Waiting (NO Token.Processed)
/// - Resume:
///     - Completion happened externally; here: Token.Processed + Node.Complete to allow navigation
///
/// Error model:
/// - Only node.Fail(message, EngineErrorKind) for handler-detected issues
/// - Token is authoritative for terminal states; node mirrors defensively
/// - Never call token.Fail(...) here
/// </summary>
public sealed class UserTaskHandler : BpmnElementHandlerBase
{
    private readonly IUserTaskService _userTaskService;
    private readonly IVariableMappingService _mapping;

    public UserTaskHandler(
        IUserTaskService userTaskService,
        IVariableMappingService mapping,
        IFeelExpressionEvaluator feel,
        ILogger<UserTaskHandler> logger)
        : base(feel, logger)
    {
        _userTaskService = userTaskService ?? throw new ArgumentNullException(nameof(userTaskService));
        _mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));
    }

    public override bool CanHandle(BpmnFlowElement element) => element is BpmnUserTask;

    public override async Task<ElementProcessResult> NodeProcessAsync(
        Process process,
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

        var userTask = (BpmnUserTask)element;
        var elementId = userTask.id ?? node.ElementId;

        Logger.LogDebug(
            "[USER-TASK] Start. P={ProcessId} T={TokenId} N={NodeId} E={ElementId} Resume={Resume} TokenState={TokenState} NodeState={NodeState} Exec={IsExec}",
            process.Id, token.Id, node.Id, elementId, isResume, token.State, node.State, node.IsExecutable);

        // ------------------------------------------------------------
        // 0) Trace / NonExecutable path
        // ------------------------------------------------------------
        if (!node.IsExecutable)
        {
            // No usertask creation; just move on
            if (token.State is not (TokenState.Terminated or TokenState.Failed))
            {
                // ensure token is in Active to call Processed (your token enforces Active)
                if (token.State == TokenState.Waiting)
                    token.Resume();

                if (token.State == TokenState.Created)
                    token.Activate();

                if (token.State == TokenState.Active)
                    token.Processed();
            }

            node.Complete();
            return ElementProcessResult.Completed;
        }

        // ------------------------------------------------------------
        // 1) Validate element id (Logical)
        // ------------------------------------------------------------
        if (string.IsNullOrWhiteSpace(elementId))
        {
            node.Fail("UserTask elementId is missing.", EngineErrorKind.Logical);
            return ElementProcessResult.Failed;
        }

        // ------------------------------------------------------------
        // 2) Terminal safety (mirror token state => node)
        // ------------------------------------------------------------
        if (token.State is TokenState.Terminated)
        {
            node.Complete();
            return ElementProcessResult.Terminated;
        }

        if (token.State is TokenState.Failed)
        {
            if (node.State != NodeState.Failed)
                node.Fail("Token is already Failed (terminal).", EngineErrorKind.Logical);

            return ElementProcessResult.NoOp;
        }

        // ------------------------------------------------------------
        // 3) Resume: external completion already happened
        // ------------------------------------------------------------
        if (isResume)
        {
            // If currently Waiting, bring it back to Active before Processed()
            if (token.State == TokenState.Waiting)
                token.Resume();

            // If for some reason token wasn't active (Created), activate defensively
            if (token.State == TokenState.Created)
                token.Activate();

            if (token.State == TokenState.Active)
                token.Processed();

            node.Complete();
            return ElementProcessResult.Completed;
        }

        // ------------------------------------------------------------
        // 4) Idempotent waiting guard
        // ------------------------------------------------------------
        if (token.State == TokenState.Waiting &&
            node.State == NodeState.Waiting &&
            node.UserTaskId is { } utid &&
            utid != Guid.Empty)
        {
            return ElementProcessResult.Waiting;
        }

        // If node already completed/failed, do nothing
        if (node.State is NodeState.Completed or NodeState.Failed or NodeState.Skipped)
            return ElementProcessResult.NoOp;

        // ------------------------------------------------------------
        // 5) First run: apply inputs once (Technical failures possible)
        // ------------------------------------------------------------
        try
        {
            token.ClearLocalVariables();
            _mapping.ApplyInputs(process, token, node, userTask, ctx);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex,
                "[USER-TASK] Input mapping failed. P={P} T={T} N={N} E={E}",
                process.Id, token.Id, node.Id, elementId);

            node.Fail("UserTask input mapping failed.", EngineErrorKind.Technical);
            return ElementProcessResult.Failed;
        }

        // ------------------------------------------------------------
        // 6) Create/Get user task (idempotent in service)
        // ------------------------------------------------------------
        Guid userTaskId;
        try
        {
            userTaskId = await _userTaskService.CreateOrGetAsync(
                process: process,
                token: token,
                node: node,
                userTask: userTask,
                ct: ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex,
                "[USER-TASK] CreateOrGetAsync failed. P={P} T={T} N={N} E={E}",
                process.Id, token.Id, node.Id, elementId);

            node.Fail("CreateOrGet user-task failed.", EngineErrorKind.Technical);
            return ElementProcessResult.Failed;
        }

        if (userTaskId == Guid.Empty)
        {
            node.Fail("CreateOrGetAsync returned empty userTaskId.", EngineErrorKind.Technical);
            return ElementProcessResult.Failed;
        }

        // ------------------------------------------------------------
        // 7) Put BOTH in waiting (no token.Processed)
        // ------------------------------------------------------------
        EnsureWaiting(token, node, userTaskId, userTask);

        Logger.LogDebug(
            "[USER-TASK] Waiting. P={P} T={T} N={N} UserTaskId={U}",
            process.Id, token.Id, node.Id, userTaskId);

        return ElementProcessResult.Waiting;
    }

    private static void EnsureWaiting(Token token, NodeInstance node, Guid userTaskId, BpmnUserTask task)
    {
        var display = task.name ?? task.id ?? node.ElementId ?? "userTask";
        var reason = $"Waiting for user task: {display}";

        // Correlation: reuse userTaskId as workerId unless you have separate worker/job id
        var workerId = userTaskId;

        // Idempotent guard
        if (token.State == TokenState.Waiting &&
            node.State == NodeState.Waiting &&
            node.WorkerId == workerId &&
            node.UserTaskId == userTaskId)
            return;

        // Token.Wait requires Active in your domain => ensure Active
        if (token.State == TokenState.Created)
            token.Activate();

        if (token.State == TokenState.Waiting)
        {
            // already waiting => keep as-is
        }
        else if (token.State == TokenState.Active)
        {
            token.Wait(reason);
        }
        else
        {
            // Any other unexpected state: be conservative and try to normalize
            // (avoid throwing and breaking the worker loop)
            token.ReActivate();
            token.Wait(reason);
        }

        node.WaitForUserTask(userTaskId: userTaskId, workerId: workerId, reason: reason);
    }
}
