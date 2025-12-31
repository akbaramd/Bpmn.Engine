using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Models.Models;
using NodeState = Novin.Bpmn.Engine.Domain.Entities.NodeState;

namespace Novin.Bpmn.Engine.Application.ElementHandlers;

/// <summary>
/// BPMN ServiceTask handler (job-inside, no mediator).
///
/// قواعد استاندارد این پیاده‌سازی:
/// - Trace/NonExecutable: هیچ Job نمی‌سازد، Node را Complete می‌کند، Token را Processed می‌کند.
/// - First run (not resume):
///     - Inputs را اعمال می‌کند
///     - اگر Job موجود بود: بسته به Status تصمیم می‌گیرد (Waiting/Complete/Fail)
///     - اگر نبود: Job می‌سازد و Token+Node را Waiting می‌کند
/// - Resume:
///     - حتماً Job را چک می‌کند (Succeeded => Outputs + Complete، Running/Pending => Waiting، Failed => Fail)
/// - Idempotency:
///     - کلید عملیاتی: (TokenId + ElementId) یا (NodeInstanceId) (ترجیحاً NodeInstanceId)
///     - برای جلوگیری از Job دوباره، Repository باید Unique Constraint داشته باشد.
/// </summary>
public sealed class ServiceTaskHandler : BpmnElementHandlerBase
{
    private readonly IWorkerRepository _workers;
    private readonly IVariableMappingService _mapping;

    public ServiceTaskHandler(
        IWorkerRepository workers,
        IVariableMappingService mapping,
        IFeelExpressionEvaluator feel,
        ILogger<ServiceTaskHandler> logger)
        : base(feel, logger)
    {
        _workers = workers ?? throw new ArgumentNullException(nameof(workers));
        _mapping = mapping ?? throw new ArgumentNullException(nameof(mapping));
    }

    public override bool CanHandle(BpmnFlowElement element)
        => element is BpmnServiceTask;

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

        var task = (BpmnServiceTask)element;

        var elementId = task.id ?? node.ElementId;
        if (string.IsNullOrWhiteSpace(elementId))
        {
            node.Fail("ServiceTask id is missing.");
            token.Fail("ServiceTask id is missing.");
            return ElementProcessResult.Failed;
        }

        // Terminal guards
        if (token.State is TokenState.Terminated or TokenState.Failed)
        {
            // Mirror into node if needed (defensive)
            if (token.State == TokenState.Failed && node.State != NodeState.Failed)
                node.Fail("Token already failed.");
            if (token.State == TokenState.Terminated && node.State != NodeState.Completed)
                node.Complete();

            return ElementProcessResult.NoOp;
        }

        // Trace/non-executable: pass-through (no job)
        if (!token.IsExecutable)
        {
            token.Processed();
            node.Complete();
            return ElementProcessResult.Completed;
        }

        // Resume path MUST be job-driven (never blindly complete)
        if (isResume)
            return await ResumeAsync(process, token, node, task, elementId, ctx, ct);

        // First run:
        // - Apply inputs once
        // - Ensure a single Job exists
        token.ClearLocalVariables();
        _mapping.ApplyInputs(process, token, task, ctx);

        var existingJob = await FindJobAsync(token, node, elementId, ct);

        if (existingJob is not null)
            return await DecideByJobStatusAsync(process, token, node, task, elementId, ctx, existingJob, ct);

        // Create job (idempotent creation relies on DB uniqueness)
        var job = Job.Create(
            processId: process.Id,
            tokenId: token.Id,
            nodeInstanceId: node.Id,
            elementId: elementId,
            taskName: task.name ?? elementId,
            implementation: task.implementation);

        try
        {
            await _workers.AddAsync(job, ct);
        }
        catch
        {
            // Another concurrent execution may have created it. Re-read and decide.
            var reread = await FindJobAsync(token, node, elementId, ct);
            if (reread is not null)
                return await DecideByJobStatusAsync(process, token, node, task, elementId, ctx, reread, ct);

            // If still not found, treat as failure.
            node.Fail("Failed to create job for service task.");
            token.Fail("Failed to create job for service task.");
            return ElementProcessResult.Failed;
        }

        EnsureWaiting(token, node, job.Id, task);
        return ElementProcessResult.Waiting;
    }

    // ------------------------- Resume -------------------------

    private async Task<ElementProcessResult> ResumeAsync(
        Process process,
        Token token,
        NodeInstance node,
        BpmnServiceTask task,
        string elementId,
        BpmnRuntimeContext ctx,
        CancellationToken ct)
    {
        var job = await FindJobAsync(token, node, elementId, ct);

        if (job is null)
        {
            // If resume requested but no job exists, safest is to go back to Waiting (or fail).
            EnsureWaiting(token, node, workerId: Guid.Empty, task);
            return ElementProcessResult.Waiting;
        }

        return await DecideByJobStatusAsync(process, token, node, task, elementId, ctx, job, ct);
    }

    private async Task<ElementProcessResult> DecideByJobStatusAsync(
        Process process,
        Token token,
        NodeInstance node,
        BpmnServiceTask task,
        string elementId,
        BpmnRuntimeContext ctx,
        Job job,
        CancellationToken ct)
    {
        switch (job.Status)
        {
            case JobStatus.Succeeded:
                // Apply outputs once and complete
                _mapping.ApplyOutputs(process, token, task, ctx);
                token.Processed();
                node.Complete();
                return ElementProcessResult.Completed;

            case JobStatus.Pending:
            case JobStatus.Running:
                EnsureWaiting(token, node, job.Id, task);
                return ElementProcessResult.Waiting;

            case JobStatus.Failed:
            case JobStatus.Canceled:
            case JobStatus.TimedOut:
                node.Fail($"Service task job ended with status '{job.Status}'. JobId={job.Id}");
                token.Fail($"Service task job ended with status '{job.Status}'. JobId={job.Id}");
                return ElementProcessResult.Failed;

            default:
                // Unknown status => conservative: wait (do not create another job)
                EnsureWaiting(token, node, job.Id, task);
                return ElementProcessResult.Waiting;
        }
    }

    // ------------------------- Job lookup -------------------------

    /// <summary>
    /// Prefer NodeInstanceId-based lookup if available; fallback to (TokenId + ElementId).
    /// Adjust repository to guarantee uniqueness for the chosen key.
    /// </summary>
    private async Task<Job?> FindJobAsync(Token token, NodeInstance node, string elementId, CancellationToken ct)
    {
        // If Node already tracks WorkerId, that is the most accurate idempotency key.
            // If your repository doesn't have GetByIdAsync, add it.
            if (node.WorkerId != null)
            {
                var byId = await _workers.GetByIdAsync(node.WorkerId.Value, ct);
                if (byId is not null) return byId;
            }

        // Fallback: TokenId + ElementId (may be unsafe for loops; prefer ActivityInstanceId/NodeId in DB key)
        return await _workers.GetByTokenAndElementAsync(token.Id, elementId, ct);
    }

    // ------------------------- Waiting -------------------------

    private static void EnsureWaiting(Token token, NodeInstance node, Guid workerId, BpmnServiceTask task)
    {
        var display = task.name ?? task.id ?? node.ElementId ?? "serviceTask";
        var reason = $"Waiting for service task: {display}";

        // Token waiting state must be consistent with node waiting state
        if (token.State != TokenState.Waiting)
            token.Wait(reason);

        // WorkerId might be Guid.Empty in some defensive paths; still set waiting.
        node.WaitForWorker(workerId, reason);
    }
}
