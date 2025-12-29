using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Commands.MoveToken;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.Events;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.EventHandlers.Task;

public sealed class WorkerCompletedEventHandler : INotificationHandler<WorkerCompletedEvent>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWorkerRepository _workerRepository;
    private readonly IVariableMappingService _variableMapping;
    private readonly IMediator _mediator;
    private readonly ILogger<WorkerCompletedEventHandler> _logger;

    public WorkerCompletedEventHandler(
        IUnitOfWork unitOfWork,
        IWorkerRepository workerRepository,
        IVariableMappingService variableMapping,
        IMediator mediator,
        ILogger<WorkerCompletedEventHandler> logger)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _workerRepository = workerRepository ?? throw new ArgumentNullException(nameof(workerRepository));
        _variableMapping = variableMapping ?? throw new ArgumentNullException(nameof(variableMapping));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async System.Threading.Tasks.Task Handle(WorkerCompletedEvent notification, CancellationToken ct)
    {
        _logger.LogInformation(
            "[WORKER-COMPLETED] Worker {WorkerId} completed for Token {TokenId} in Process {ProcessId}",
            notification.WorkerId, notification.TokenId, notification.ProcessId);

        await _unitOfWork.ExecuteInTransactionAsync(async trxCt =>
        {
            var token = await _unitOfWork.Tokens.GetByIdAsync(notification.TokenId, trxCt);
            if (token == null)
            {
                _logger.LogWarning("[WORKER-COMPLETED] Token {TokenId} not found", notification.TokenId);
                return;
            }

            var process = await _unitOfWork.Processes.GetByIdAsync(notification.ProcessId, trxCt);
            if (process == null)
            {
                _logger.LogWarning("[WORKER-COMPLETED] Process {ProcessId} not found", notification.ProcessId);
                return;
            }

            var worker = await _workerRepository.GetByIdAsync(notification.WorkerId, trxCt);
            if (worker == null)
            {
                _logger.LogWarning("[WORKER-COMPLETED] Worker {WorkerId} not found", notification.WorkerId);
                return;
            }

            // Check if worker is still in a state that can be completed
            if (worker.Status != Novin.Bpmn.Engine.Domain.Entities.WorkerStatus.InProgress)
            {
                _logger.LogWarning(
                    "[WORKER-COMPLETED] Worker {WorkerId} is already in final state {Status}, ignoring completion event",
                    notification.WorkerId, worker.Status);
                return;
            }

            // Update token with worker results
            if (notification.Result != null)
            {
                foreach (var (key, value) in notification.Result)
                {
                    token.SetVariable(key, value);
                }
            }

            // Create runtime context for variable mapping
            var deployment = await _unitOfWork.Deployments.GetByIdAsync(process.DeploymentId, trxCt);
            if (deployment == null)
            {
                _logger.LogWarning("[WORKER-COMPLETED] Deployment not found for {DeploymentId}", process.DeploymentId);
                return;
            }

            var definitionsService = new BpmnDefinitionsService(deployment.GetDefinitions());
            var modelAccessor = new BpmnModelAccessor(definitionsService);
            var ctx = new BpmnRuntimeContext(
                BpmnProcessId: process.ProcessBpmnId,
                Model: modelAccessor);

            // Get the current element for output mapping
            var currentElement = ctx.Model.GetElementById(process.ProcessBpmnId, token.CurrentElementId);
            if (currentElement == null)
            {
                _logger.LogWarning("[WORKER-COMPLETED] Current element {ElementId} not found", token.CurrentElementId);
                return;
            }

            // Apply output mapping (Token → Process)
            _variableMapping.ApplyOutputs(process, token, currentElement, ctx);

            // Navigate to next element
            var outgoing = ctx.Model.GetOutgoingSequenceFlows(process.ProcessBpmnId, token.CurrentElementId);
            if (outgoing.Count == 0)
            {
                _logger.LogWarning("[WORKER-COMPLETED] No outgoing flow from element. ProcessId={ProcessId} TokenId={TokenId} ElementId={ElementId}",
                    process.Id, token.Id, token.CurrentElementId);
                return;
            }

            if (outgoing.Count > 1)
            {
                _logger.LogWarning("[WORKER-COMPLETED] Multiple outgoing flows (should be 1). ProcessId={ProcessId} TokenId={TokenId} ElementId={ElementId} Count={Count}",
                    process.Id, token.Id, token.CurrentElementId, outgoing.Count);
            }

            var selectedFlow = outgoing[0];
            if (string.IsNullOrWhiteSpace(selectedFlow.targetRef))
            {
                _logger.LogError("[WORKER-COMPLETED] Selected flow has null/empty targetRef. ProcessId={ProcessId} TokenId={TokenId} FlowId={FlowId}",
                    process.Id, token.Id, selectedFlow.id);
                return;
            }

            _logger.LogDebug("[WORKER-COMPLETED] Moving token to next element. From={FromElementId} To={ToElementId} ViaFlow={ViaFlowId}",
                token.CurrentElementId, selectedFlow.targetRef, selectedFlow.id);

            await _mediator.Send(new MoveTokenCommand(
                ProcessId: process.Id,
                TokenId: token.Id,
                NextElementId: selectedFlow.targetRef,
                ViaFlowId: selectedFlow.id), ct);
        }, ct);
    }
}