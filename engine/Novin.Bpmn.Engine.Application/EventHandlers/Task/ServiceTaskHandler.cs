using Microsoft.Extensions.Logging;
using MediatR;
using Novin.Bpmn.Engine.Application.Commands.WaitToken;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.EventHandlers;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.ElementHandlers;

/// <summary>
/// BPMN ServiceTask handler (async worker-based).
///
/// Semantics:
/// - Executable token:
///   - Apply input mapping
///   - Create worker
///   - Wait token
///
/// - Resume:
///   - Output mapping already applied
///   - Just complete element
///
/// - Trace token:
///   - No worker
///   - Auto-complete
/// </summary>
public sealed class ServiceTaskHandler : BpmnElementHandlerBase
{
    private readonly IWorkerRepository _workerRepository;
    private readonly IVariableMappingService _variableMapping;

    public ServiceTaskHandler(
        IWorkerRepository workerRepository,
        IVariableMappingService variableMapping,
        IMediator mediator,
        IFeelExpressionEvaluator feel,
        ILogger<ServiceTaskHandler> logger)
        : base(mediator, feel, logger)
    {
        _workerRepository = workerRepository ?? throw new ArgumentNullException(nameof(workerRepository));
        _variableMapping = variableMapping ?? throw new ArgumentNullException(nameof(variableMapping));
    }

    public override bool CanHandle(BpmnFlowElement element)
        => element is BpmnServiceTask;

    public override async Task<ElementProcessResult> ProcessAsync(
        Process process,
        Token token,
        BpmnFlowElement element,
        BpmnRuntimeContext ctx,
        bool isResume,
        CancellationToken ct)
    {
        var serviceTask = (BpmnServiceTask)element;

        Logger.LogDebug(
            "[SERVICE] Enter ProcessAsync. TokenId={TokenId} Exec={Exec} Resume={Resume}",
            token.Id, token.IsExecutable, isResume);

        // --------------------------------------------------
        // 1) Input mapping (only once, only executable)
        // --------------------------------------------------
        if (token.IsExecutable && !isResume)
        {
            token.ClearLocalVariables();
            _variableMapping.ApplyInputs(process, token, element, ctx);
        }

        // --------------------------------------------------
        // 2) Trace token → auto-complete
        // --------------------------------------------------
        if (!token.IsExecutable)
        {
            Logger.LogDebug(
                "[SERVICE] Trace token → skipping worker. TokenId={TokenId}",
                token.Id);

            // ✅ Mark token as processed (NodeDone) even for trace tokens
            token.Processed();

            return ElementProcessResult.Completed;
        }

        // --------------------------------------------------
        // 3) Resume path (worker already completed)
        // --------------------------------------------------
        if (isResume)
        {
            Logger.LogInformation(
                "[SERVICE] Resume after worker completion. TokenId={TokenId}",
                token.Id);

            // Output mapping already applied in ResumeWorker handler
            // ✅ Mark token as processed (NodeDone) after resume
            token.Processed();

            return ElementProcessResult.Completed;
        }

        // --------------------------------------------------
        // 4) Normal execution → create / check worker
        // --------------------------------------------------
        var existingWorker = await _workerRepository.GetByTokenIdAsync(token.Id);

        if (existingWorker != null)
        {
            switch (existingWorker.Status)
            {
                case WorkerStatus.Completed:
                    Logger.LogInformation(
                        "[SERVICE] Worker already completed. TokenId={TokenId}",
                        token.Id);

                    _variableMapping.ApplyOutputs(process, token, element, ctx);
                    
                    // ✅ Mark token as processed (NodeDone) after output mapping
                    token.Processed();

                    return ElementProcessResult.Completed;

                case WorkerStatus.Pending:
                case WorkerStatus.InProgress:
                    Logger.LogInformation(
                        "[SERVICE] Worker still running → waiting. TokenId={TokenId}",
                        token.Id);

                    return ElementProcessResult.Waiting;

                default:
                    Logger.LogWarning(
                        "[SERVICE] Worker in {Status} → recreating. TokenId={TokenId}",
                        existingWorker.Status, token.Id);
                    break;
            }
        }

        // --------------------------------------------------
        // 5) Create new worker
        // --------------------------------------------------
        var routing = ParseClientRouting(serviceTask);

        var worker = Job.CreateServiceTask(
            processId: process.Id,
            tokenId: token.Id,
            elementId: serviceTask.id!,
            taskName: serviceTask.name ?? serviceTask.id!,
            spec: new ServiceTaskSpec( routing.CleanImplementation ?? string.Empty,routing.ClientId ?? string.Empty),
             token.Variables);

        await _workerRepository.AddAsync(worker, ct);

        await Mediator.Send(new WaitTokenCommand(
            ProcessId: process.Id,
            TokenId: token.Id,
            Reason: $"Waiting for service task: {serviceTask.name}",
            WorkerId: worker.Id), ct);

        Logger.LogInformation(
            "[SERVICE] Worker created and token waiting. WorkerId={WorkerId} TokenId={TokenId}",
            worker.Id, token.Id);

        return ElementProcessResult.Waiting;
    }
    
    private static ClientRoutingInfo ParseClientRouting(BpmnServiceTask serviceTask)
    {
        var info = new ClientRoutingInfo();

        if (serviceTask == null)
            return info;

        var impl = serviceTask.implementation?.Trim();

        if (string.IsNullOrEmpty(impl))
            return info;

        // ----------------------------------------
        // Split client@rest
        // ----------------------------------------
        string? clientPart = null;
        string handlerPart = impl;

        var atIndex = impl.IndexOf('@');
        if (atIndex >= 0)
        {
            clientPart = impl[..atIndex].Trim();
            handlerPart = impl[(atIndex + 1)..].Trim();
        }

        if (!string.IsNullOrWhiteSpace(clientPart))
            info.ClientId = clientPart;

        // ----------------------------------------
        // Split handler:timeout
        // ----------------------------------------
        var colonIndex = handlerPart.LastIndexOf(':');
        if (colonIndex > 0 && colonIndex < handlerPart.Length - 1)
        {
            var handler = handlerPart[..colonIndex].Trim();
            var timeoutRaw = handlerPart[(colonIndex + 1)..].Trim();

            if (!string.IsNullOrWhiteSpace(handler))
                info.CleanImplementation = handler;

            if (int.TryParse(timeoutRaw, out var ms) && ms > 0)
                info.TimeoutSeconds = ms/1000;
        }
        else
        {
            info.CleanImplementation = handlerPart;
        }

        return info;
    }
}
