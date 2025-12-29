using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.EventHandlers;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Models.Models;
using NodeState = Novin.Bpmn.Engine.Domain.Entities.NodeState;

namespace Novin.Bpmn.Engine.Application.ElementHandlers;

/// <summary>
/// BPMN ServiceTask handler (job-inside, NO mediator/commands).
///
/// ✅ Creates/checks Job inside handler
/// ✅ Sets Token.Wait + Node.Wait directly
/// ✅ Idempotent by (TokenId + ElementId)
/// ✅ Resume path applies outputs + token.Processed()
///
/// IMPORTANT:
/// - This handler assumes it runs inside a UnitOfWork transaction (outer dispatcher/command handler),
///   so calling _workers.AddAsync(...) and token/node state changes are persisted together.
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

    public override async Task<ElementProcessResult> ProcessAsync(
        Process process,
        Token token,
        NodeInstance node,
        BpmnFlowElement element,
        BpmnRuntimeContext ctx,
        bool isResume,
        CancellationToken ct)
    {
        var serviceTask = (BpmnServiceTask)element;
        var elementId = serviceTask.id ?? node.ElementId;

        Logger.LogDebug(
            "[SERVICE] Process. ProcessId={ProcessId} TokenId={TokenId} NodeId={NodeId} ElementId={ElementId} Exec={Exec} Resume={Resume} TokenState={TokenState} NodeState={NodeState}",
            process.Id, token.Id, node.Id, elementId, token.IsExecutable, isResume, token.State, node.State);

        // Guard: terminal => no-op
        if (token.State is TokenState.Terminated or TokenState.Failed)
            return ElementProcessResult.NoOp;

        // Trace token => pass-through
        if (!token.IsExecutable)
        {
            token.Processed();
            return ElementProcessResult.Completed;
        }

        // Resume => outputs + processed
        if (isResume)
        {
            _mapping.ApplyOutputs(process, token, serviceTask, ctx);
            token.Processed();
            return ElementProcessResult.Completed;
        }

        // First-run: inputs (only once)
        token.ClearLocalVariables();
        _mapping.ApplyInputs(process, token, serviceTask, ctx);

        // Idempotency: keyed by (TokenId + ElementId)
        var existing = await _workers.GetByTokenAndElementAsync(token.Id, elementId!, ct);

        if (existing is not null)
        {
            switch (existing.Status)
            {
                case JobStatus.Succeeded:
                    _mapping.ApplyOutputs(process, token, serviceTask, ctx);
                    token.Processed();
                    return ElementProcessResult.Completed;

                case JobStatus.Pending:
                case JobStatus.Running:
                    EnsureWaiting(token, node, existing.Id, serviceTask);
                    return ElementProcessResult.Waiting;

                default:
                    Logger.LogWarning(
                        "[SERVICE] Existing job in {Status} => recreate. TokenId={TokenId} ElementId={ElementId} JobId={JobId}",
                        existing.Status, token.Id, elementId, existing.Id);
                    break;
            }
        }

        // Create new job
        var routing = ParseClientRouting(serviceTask);

        var job = Job.Create(
            processId: process.Id,
            tokenId: token.Id,
            nodeInstanceId:node.Id,
            elementId: elementId!,
            taskName: serviceTask.name ?? elementId!,
            serviceTask.implementation);

        await _workers.AddAsync(job, ct);

        // Put token+node to Waiting (NO mediator)
        EnsureWaiting(token, node, job.Id, serviceTask);

        Logger.LogInformation(
            "[SERVICE] Job created & waiting. TokenId={TokenId} NodeId={NodeId} ElementId={ElementId} JobId={JobId}",
            token.Id, node.Id, elementId, job.Id);

        return ElementProcessResult.Waiting;
    }

    private void EnsureWaiting(Token token, NodeInstance node, Guid workerId, BpmnServiceTask task)
    {
        var reason = $"Waiting for service task: {task.name ?? task.id ?? node.ElementId}";

        // Make idempotent: if already waiting for same worker, do nothing
        if (token.State == TokenState.Waiting  &&
            node.State == NodeState.Waiting && node.WorkerId == workerId)
            return;


        // Node.Wait should set:
        // - State=Waiting
        // - WorkerId=workerId
        // - Reason=reason
        node.WaitForWorker(workerId, reason);
    }

    private static ClientRoutingInfo ParseClientRouting(BpmnServiceTask serviceTask)
    {
        var info = new ClientRoutingInfo();

        var impl = serviceTask.implementation?.Trim();
        if (string.IsNullOrEmpty(impl))
            return info;

        // client@handler:timeout
        string? clientPart = null;
        var handlerPart = impl;

        var atIndex = impl.IndexOf('@');
        if (atIndex >= 0)
        {
            clientPart = impl[..atIndex].Trim();
            handlerPart = impl[(atIndex + 1)..].Trim();
        }

        if (!string.IsNullOrWhiteSpace(clientPart))
            info.ClientId = clientPart;

        var colonIndex = handlerPart.LastIndexOf(':');
        if (colonIndex > 0 && colonIndex < handlerPart.Length - 1)
        {
            var handler = handlerPart[..colonIndex].Trim();
            var timeoutRaw = handlerPart[(colonIndex + 1)..].Trim();

            if (!string.IsNullOrWhiteSpace(handler))
                info.CleanImplementation = handler;

            if (int.TryParse(timeoutRaw, out var ms) && ms > 0)
                info.TimeoutSeconds = ms / 1000;
        }
        else
        {
            info.CleanImplementation = handlerPart;
        }

        return info;
    }
}
