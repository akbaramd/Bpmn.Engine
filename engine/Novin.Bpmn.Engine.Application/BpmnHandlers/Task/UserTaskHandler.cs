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
/// BPMN UserTask handler (standard engine semantics, no mediator).
///
/// قواعد:
/// - NonExecutable/Trace: هیچ UserTask نمی‌سازد، Node را Complete می‌کند، Token را Processed می‌کند.
/// - First run:
///     - Inputs اعمال می‌شود
///     - CreateOrGet (idempotent) ایجاد می‌شود
///     - Token+Node به Waiting می‌روند (بدون token.Processed)
/// - Resume:
///     - فرض: completion بیرون از انجین انجام شده و outputs همانجا اعمال شده
///     - اینجا فقط Node را Complete و Token را Processed می‌کنیم تا Navigation انجام شود
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

        if (string.IsNullOrWhiteSpace(elementId))
        {
            node.Fail("UserTask elementId is missing.");
            token.Fail("UserTask elementId is missing.");
            return ElementProcessResult.Failed;
        }

        Logger.LogDebug(
            "[USER-TASK] Process. P={ProcessId} T={TokenId} N={NodeId} E={ElementId} Exec={Exec} Resume={Resume} TokenState={TokenState} NodeState={NodeState}",
            process.Id, token.Id, node.Id, elementId, token.IsExecutable, isResume, token.State, node.State);

        // Terminal safety
        if (token.State is TokenState.Terminated or TokenState.Failed)
        {
            if (token.State == TokenState.Failed && node.State != NodeState.Failed)
                node.Fail("Token already failed.");
            if (token.State == TokenState.Terminated && node.State != NodeState.Completed)
                node.Complete();

            return ElementProcessResult.NoOp;
        }

        // Trace/non-executable => pass-through (no task created)
        if (!token.IsExecutable)
        {
            token.Processed();
            node.Complete();
            return ElementProcessResult.Completed;
        }

        // Resume => task completed externally; allow navigation
        if (isResume)
        {
            token.Processed();
            node.Complete();
            return ElementProcessResult.Completed;
        }

        // If already waiting for a user task and correlation exists => keep waiting (idempotent)
        if (token.State == TokenState.Waiting && node.State == NodeState.Waiting && node.UserTaskId != Guid.Empty)
            return ElementProcessResult.Waiting;

        // First run: inputs (only once)
        token.ClearLocalVariables();
        _mapping.ApplyInputs(process, token, userTask, ctx);

        // Create/Get user-task (MUST be idempotent inside the service)
        // Recommended correlation key: (process.Id, token.Id, node.Id, elementId)
        var userTaskId = await _userTaskService.CreateOrGetAsync(
            process: process,
            token: token,
            node: node,
            userTask: userTask,
            ct: ct);

        if (userTaskId == Guid.Empty)
        {
            node.Fail("CreateOrGetAsync returned empty userTaskId.");
            token.Fail("CreateOrGetAsync returned empty userTaskId.");
            return ElementProcessResult.Failed;
        }

        EnsureWaiting(token, node, userTaskId, userTask);
        return ElementProcessResult.Waiting;
    }

    private static void EnsureWaiting(Token token, NodeInstance node, Guid userTaskId, BpmnUserTask task)
    {
        var display = task.name ?? task.id ?? node.ElementId ?? "userTask";
        var reason = $"Waiting for user task: {display}";

        // Correlation: reuse userTaskId as workerId unless you have a separate worker/job id
        var workerId = userTaskId;

        // Idempotent guard
        if (token.State == TokenState.Waiting &&
            node.State == NodeState.Waiting &&
            node.WorkerId == workerId &&
            node.UserTaskId == userTaskId)
            return;

        // Put BOTH in waiting (do NOT call token.Processed)
        if (token.State != TokenState.Waiting)
            token.Wait(reason);

        node.WaitForUserTask(userTaskId: userTaskId, workerId: workerId, reason: reason);
    }
}
