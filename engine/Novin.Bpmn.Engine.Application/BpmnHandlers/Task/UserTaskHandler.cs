using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.ElementHandlers;

/// <summary>
/// BPMN UserTask handler (production-ready, job/task INSIDE handler).
///
/// ✅ Creates/checks UserTask inside handler (idempotent by TokenId+ElementId or NodeId)
/// ✅ Sets Token.Wait + Node.Wait directly (NO Mediator/Commands)
/// ✅ Does NOT call token.Processed() when returning Waiting (important!)
///
/// Resume semantics:
/// - isResume=true means: UserTask was completed by external API, token/node already released.
/// - Here we only mark token as processed so dispatcher can navigate.
///   (Output mapping should be applied by "CompleteUserTask" application service/command.)
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

    public override async Task<ElementProcessResult> ProcessAsync(
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
            "[USER-TASK] Process. P={ProcessId} T={TokenId} N={NodeId} E={ElementId} Exec={Exec} Resume={Resume} TokenState={TokenState} NodeState={NodeState}",
            process.Id, token.Id, node.Id, elementId, token.IsExecutable, isResume, token.State, node.State);

        // terminal safety
        if (token.State is TokenState.Terminated or TokenState.Failed)
            return ElementProcessResult.NoOp;

        // ------------------------------------------------------------------
        // Trace token => do not create user task, just flow through.
        // (You may still want to record node completed; that's dispatcher’s job.)
        // ------------------------------------------------------------------
        if (!token.IsExecutable)
        {
            token.Processed(); // ok: trace should navigate
            return ElementProcessResult.Completed;
        }

        // ------------------------------------------------------------------
        // Resume => UserTask already completed externally, outputs applied there.
        // Just allow navigation.
        // ------------------------------------------------------------------
        if (isResume)
        {
            token.Processed();
            return ElementProcessResult.Completed;
        }

        // ------------------------------------------------------------------
        // First execution:
        // 1) Apply inputs
        // 2) Create/Get user-task (idempotent)
        // 3) Put token+node into Waiting (NO token.Processed!)
        // ------------------------------------------------------------------
        token.ClearLocalVariables();
        _mapping.ApplyInputs(process, token, userTask, ctx);

        // IMPORTANT: your service MUST be idempotent:
        // - If a task already exists for (TokenId + ElementId) or (NodeId), return same WorkerId/TaskId.
        // - Otherwise create a new task.
        //
        // Recommended idempotency key:
        //   (process.Id, token.Id, node.Id, elementId)
        //
        // Return value: workerId (or taskId) used for correlation/resume.
        var userTaskId = await _userTaskService.CreateOrGetAsync(
            process: process,
            token: token,
            node: node,
            userTask: userTask,
            ct: ct);

        if (userTaskId == Guid.Empty)
        {
            Logger.LogWarning(
                "[USER-TASK] CreateOrGetAsync returned empty userTaskId. P={ProcessId} T={TokenId} N={NodeId} E={ElementId}",
                process.Id, token.Id, node.Id, elementId);

            return ElementProcessResult.Failed;
        }

        EnsureWaiting(process, token, node, userTaskId, userTask);

        Logger.LogInformation(
            "[USER-TASK] Waiting. P={ProcessId} T={TokenId} N={NodeId} E={ElementId} UserTaskId={UserTaskId}",
            process.Id, token.Id, node.Id, elementId, userTaskId);

        return ElementProcessResult.Waiting;
    }

    /// <summary>
    /// For UserTask, navigation must NOT run while waiting.
    /// Base NavigateAsync already stops on Waiting, so we keep it.
    /// </summary>
    public override Task NavigateAsync(
        Process process,
        Token token,
        NodeInstance nodeInstance,
        BpmnFlowElement element,
        BpmnRuntimeContext ctx,
        bool isResume,
        CancellationToken ct)
        => base.NavigateAsync(process, token, nodeInstance, element, ctx, isResume, ct);

    private void EnsureWaiting(
        Process process,
        Token token,
        NodeInstance node,
        Guid userTaskId,
        BpmnUserTask task)
    {
        var reason = $"Waiting for user task: {task.name ?? task.id ?? node.ElementId}";

        // If you don't have a separate "worker/job id" for user task, reuse userTaskId as workerId.
        // This keeps correlation simple and still satisfies token.Wait(workerId, userTaskId, ...).
        var workerId = userTaskId;

        // Idempotent: already waiting on same correlation => no-op
        if (token.State == TokenState.Waiting &&
            node.State == Domain.Entities.NodeState.Waiting &&
            node.WorkerId == workerId &&
            node.UserTaskId == userTaskId)
            return;

        // Put BOTH in waiting (do NOT call token.Processed here)
        node.WaitForUserTask(userTaskId: userTaskId, workerId: workerId, reason: reason);
    }
}
