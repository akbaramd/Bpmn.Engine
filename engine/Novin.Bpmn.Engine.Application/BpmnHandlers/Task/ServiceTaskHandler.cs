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
/// Production-ready BPMN ServiceTask handler (job-backed, no mediator).
///
/// Key semantics:
/// - NonExecutable/Trace (node.IsExecutable == false):
///     - DO NOT create Job
///     - Complete node
///     - Token.Processed() (best-effort normalize token state)
///
/// - First run (isResume == false):
///     - Apply inputs once
///     - Find existing Job (idempotency)
///     - If none, create Job (DB uniqueness required)
///     - Token+Node => Waiting (NO token.Processed)
///
/// - Resume (isResume == true):
///     - MUST be job-driven
///     - If job missing => Logical failure
///     - Decide by job status
///
/// Error model:
/// - Only node.Fail(message, EngineErrorKind) from this handler (NO token.Fail here)
/// - token state is mirrored defensively into node for terminal states
/// - BPMN error semantics (EngineErrorKind.BpmnError) is produced only if you can map something
///   to BPMN ErrorCode (e.g., from job failure reason), otherwise Technical/Logical.
///
/// NOTE: token.Wait() requires TokenState.Active in your Token aggregate.
///       EnsureWaiting() normalizes token state before calling token.Wait().
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

    public override bool CanHandle(BpmnFlowElement element) => element is BpmnServiceTask;

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

        Logger.LogDebug(
            "[SERVICE-TASK] Start. P={P} T={T} N={N} E={E} Resume={Resume} TokenState={TokenState} NodeState={NodeState} Exec={IsExec}",
            process.Id, token.Id, node.Id, elementId, isResume, token.State, node.State, node.IsExecutable);

        // ------------------------------------------------------------
        // 0) Trace / NonExecutable path
        // ------------------------------------------------------------
        if (!node.IsExecutable)
        {
            Logger.LogDebug("[SERVICE-TASK] NonExecutable/Trace => skip job creation. N={N}", node.Id);

            BestEffortTokenProcessed(token);
            node.Complete();
            return ElementProcessResult.Completed;
        }

        // ------------------------------------------------------------
        // 1) Validate element id (Logical)
        // ------------------------------------------------------------
        if (string.IsNullOrWhiteSpace(elementId))
        {
            node.Fail("ServiceTask elementId is missing.", EngineErrorKind.Logical);
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
        // 3) Resume path MUST be job-driven
        // ------------------------------------------------------------
        if (isResume)
            return await ResumeAsync(process, token, node, task, elementId, ctx, ct).ConfigureAwait(false);

        // ------------------------------------------------------------
        // 4) First run: apply inputs once (mapping errors are Technical)
        // ------------------------------------------------------------
        try
        {
            token.ClearLocalVariables();
            _mapping.ApplyInputs(process, token, node, task, ctx);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex,
                "[SERVICE-TASK] Input mapping failed. P={P} T={T} N={N} E={E}",
                process.Id, token.Id, node.Id, elementId);

            node.Fail("ServiceTask input mapping failed.", EngineErrorKind.Technical);
            return ElementProcessResult.Failed;
        }

        // ------------------------------------------------------------
        // 5) Find existing job (idempotency)
        // ------------------------------------------------------------
        Job? existingJob;
        try
        {
            existingJob = await FindJobAsync(token, node, elementId, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex,
                "[SERVICE-TASK] Job lookup failed. P={P} T={T} N={N} E={E}",
                process.Id, token.Id, node.Id, elementId);

            node.Fail("Failed to lookup existing job for service task.", EngineErrorKind.Technical);
            return ElementProcessResult.Failed;
        }

        if (existingJob is not null)
            return await DecideByJobStatusAsync(process, token, node, task, elementId, ctx, existingJob, ct)
                .ConfigureAwait(false);

        // ------------------------------------------------------------
        // 6) Create job (DB uniqueness must guarantee idempotency)
        // ------------------------------------------------------------
        var job = Job.Create(
            processId: process.Id,
            tokenId: token.Id,
            nodeInstanceId: node.Id,
            elementId: elementId,
            taskName: task.name ?? elementId,
            implementation: task.implementation);

        try
        {
            await _workers.AddAsync(job, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Possibly duplicate insert due to concurrency => re-read and decide.
            Logger.LogWarning(ex,
                "[SERVICE-TASK] AddAsync failed; retrying lookup. P={P} T={T} N={N} E={E}",
                process.Id, token.Id, node.Id, elementId);

            Job? reread = null;
            try
            {
                reread = await FindJobAsync(token, node, elementId, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception lookupEx)
            {
                Logger.LogError(lookupEx,
                    "[SERVICE-TASK] Re-lookup after AddAsync failure also failed. P={P} T={T} N={N} E={E}",
                    process.Id, token.Id, node.Id, elementId);
            }

            if (reread is not null)
                return await DecideByJobStatusAsync(process, token, node, task, elementId, ctx, reread, ct)
                    .ConfigureAwait(false);

            node.Fail("Failed to create job for service task.", EngineErrorKind.Technical);
            return ElementProcessResult.Failed;
        }

        // ------------------------------------------------------------
        // 7) Put into Waiting (NO token.Processed)
        // ------------------------------------------------------------
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
        Job? job;
        try
        {
            job = await FindJobAsync(token, node, elementId, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex,
                "[SERVICE-TASK] Resume job lookup failed. P={P} T={T} N={N} E={E}",
                process.Id, token.Id, node.Id, elementId);

            node.Fail("Resume requested but job lookup failed.", EngineErrorKind.Technical);
            return ElementProcessResult.Failed;
        }

        if (job is null)
        {
            // Resume requested but no job exists => Logical inconsistency
            node.Fail("Resume requested but no job exists for this service task.", EngineErrorKind.Logical);
            return ElementProcessResult.Failed;
        }

        return await DecideByJobStatusAsync(process, token, node, task, elementId, ctx, job, ct)
            .ConfigureAwait(false);
    }

    // ------------------------- Decide by Job -------------------------

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
        // Defensive: if node is already terminal, don't re-run side-effects
        if (node.State is NodeState.Completed or NodeState.Failed or NodeState.Skipped)
            return ElementProcessResult.NoOp;

        switch (job.Status)
        {
            case JobStatus.Succeeded:
            {
                try
                {
                    _mapping.ApplyOutputs(process, token, node, task, ctx);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex,
                        "[SERVICE-TASK] Output mapping failed. P={P} T={T} N={N} JobId={JobId} E={E}",
                        process.Id, token.Id, node.Id, job.Id, elementId);

                    node.Fail("ServiceTask output mapping failed.", EngineErrorKind.Technical);
                    return ElementProcessResult.Failed;
                }

                // Complete processing (success path)
                token.Processed();
                node.Complete();
                return ElementProcessResult.Completed;
            }

            case JobStatus.Pending:
            case JobStatus.Running:
                EnsureWaiting(token, node, job.Id, task);
                return ElementProcessResult.Waiting;

            case JobStatus.TimedOut:
                node.Fail($"ServiceTask job timed out. JobId={job.Id}", EngineErrorKind.Technical);
                return ElementProcessResult.Failed;

            case JobStatus.Failed:
            {
                // If your Job has BPMN error semantics (e.g. ErrorCode), map it here:
                // - If catchable BPMN error => EngineErrorKind.BpmnError
                // - Otherwise => Technical
                var kind = TryMapToBpmnError(job) ? EngineErrorKind.BpmnError : EngineErrorKind.Technical;
                node.Fail($"ServiceTask job failed. JobId={job.Id}", kind);
                return ElementProcessResult.Failed;
            }

            case JobStatus.Canceled:
                // Usually a "logical/operational" action (manual cancel)
                node.Fail($"ServiceTask job was canceled. JobId={job.Id}", EngineErrorKind.Logical);
                return ElementProcessResult.Failed;

            default:
                node.Fail($"ServiceTask job has unknown status '{job.Status}'. JobId={job.Id}", EngineErrorKind.Technical);
                return ElementProcessResult.Failed;
        }
    }

    // ------------------------- Job lookup -------------------------

    private async Task<Job?> FindJobAsync(Token token, NodeInstance node, string elementId, CancellationToken ct)
    {
        // Prefer exact correlation via node.WorkerId (strong idempotency)
        if (node.WorkerId is { } wid && wid != Guid.Empty)
        {
            var byId = await _workers.GetByIdAsync(wid, ct).ConfigureAwait(false);
            if (byId is not null) return byId;
        }

        // Fallback: TokenId + ElementId
        return await _workers.GetByTokenAndElementAsync(token.Id, elementId, ct).ConfigureAwait(false);
    }

    // ------------------------- Waiting -------------------------

    private static void EnsureWaiting(Token token, NodeInstance node, Guid workerId, BpmnServiceTask task)
    {
        var display = task.name ?? task.id ?? node.ElementId ?? "serviceTask";
        var reason = $"Waiting for service task: {display}";

        // token.Wait requires Active => normalize
        if (token.State == TokenState.Created)
            token.Activate();

        if (token.State == TokenState.Waiting)
        {
            // already waiting
        }
        else if (token.State == TokenState.Active)
        {
            token.Wait(reason);
        }
        else
        {
            // unexpected but non-terminal => normalize best-effort
            token.ReActivate();
            token.Wait(reason);
        }

        node.WaitForWorker(workerId, reason);
    }

    // ------------------------- Helpers -------------------------

    private static void BestEffortTokenProcessed(Token token)
    {
        if (token.State is TokenState.Terminated or TokenState.Failed)
            return;

        // token.Processed requires Active; normalize
        if (token.State == TokenState.Waiting)
            token.Resume();

        if (token.State == TokenState.Created)
            token.Activate();

        if (token.State == TokenState.Active)
            token.Processed();
    }

    /// <summary>
    /// If your Job model supports BPMN error code (catchable), detect it here.
    /// Right now this returns false because your provided Job API doesn't show ErrorCode.
    /// Implement when you add something like job.ErrorType/job.ErrorCode.
    /// </summary>
    private static bool TryMapToBpmnError(Job job)
    {
        // مثال اگر اضافه کردی:
        // return job.ErrorType == ErrorType.BpmnError && !string.IsNullOrWhiteSpace(job.ErrorCode);
        return false;
    }
}
