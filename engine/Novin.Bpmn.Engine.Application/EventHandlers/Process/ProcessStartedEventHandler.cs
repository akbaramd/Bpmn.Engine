using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Commands.CreateToken;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Events;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

public sealed class ProcessStartedEventHandler : INotificationHandler<ProcessStartedEvent>
{
    private readonly IUnitOfWork _uow;
    private readonly IMediator _mediator;
    private readonly IProcessExecutionRecorder _executionRecorder;
    private readonly ILogger<ProcessStartedEventHandler> _logger;

    public ProcessStartedEventHandler(
        IUnitOfWork uow,
        IMediator mediator,
        IProcessExecutionRecorder executionRecorder,
        ILogger<ProcessStartedEventHandler> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _executionRecorder = executionRecorder ?? throw new ArgumentNullException(nameof(executionRecorder));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async System.Threading.Tasks.Task Handle(ProcessStartedEvent @event, CancellationToken ct)
    {
        // tokenIds created for start events (to record execution after commit)
        var createdStartTokens = new List<(Guid TokenId, string StartElementId)>(capacity: 4);

        await _uow.ExecuteInTransactionAsync(async txCt =>
        {
            _logger.LogInformation(
                "[PROCESS-STARTED] Handling. ProcessId={ProcessId} StartedAt={StartedAt}",
                @event.ProcessId, @event.StartedAt);

            var process = await _uow.Processes.GetByIdAsync(@event.ProcessId, txCt);
            if (process is null)
            {
                _logger.LogWarning("[PROCESS-STARTED] Process not found. ProcessId={ProcessId}", @event.ProcessId);
                return;
            }

            // Guard: اگر پروسس قبلاً بسته شده، توکن start نساز
            if (process.State is not ProcessState.Running and not ProcessState.Created)
            {
                _logger.LogWarning(
                    "[PROCESS-STARTED] Process not in Running/Created. Skip. ProcessId={ProcessId} State={State}",
                    process.Id, process.State);
                return;
            }

            var deployment = await _uow.Deployments.GetByIdAsync(process.DeploymentId, txCt);
            if (deployment is null)
            {
                _logger.LogWarning("[PROCESS-STARTED] Deployment not found. DeploymentId={DeploymentId}", process.DeploymentId);
                return;
            }

            var defs = deployment.GetDefinitions();
            var defsService = new BpmnDefinitionsService(defs);

            var bpmnProcessId = process.ProcessBpmnId;

            var startEvents = defsService.GetStartEvents(bpmnProcessId)
                .Where(se => !string.IsNullOrWhiteSpace(se.id))
                .ToList();

            if (startEvents.Count == 0)
            {
                _logger.LogWarning("[PROCESS-STARTED] No start events found. BpmnProcessId={BpmnProcessId}", bpmnProcessId);
                return;
            }

            // idempotency: اگر قبلاً توکن زنده روی start داریم، دوباره نساز
            var allTokens = await _uow.Tokens.GetByProcessIdAsync(process.Id, txCt);

            var aliveByElement = allTokens
                .Where(t => t.State is not TokenState.Completed
                         && t.State is not TokenState.Terminated
                         && t.State is not TokenState.Failed)
                .GroupBy(t => t.CurrentElementId)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

            var created = 0;

            foreach (var se in startEvents)
            {
                var startId = se.id!;

                if (aliveByElement.TryGetValue(startId, out var existing) && existing.Count > 0)
                {
                    _logger.LogInformation(
                        "[PROCESS-STARTED] Start token already exists. ProcessId={ProcessId} StartId={StartId} Count={Count}",
                        process.Id, startId, existing.Count);
                    continue;
                }

                var createResult = await _mediator.Send(new CreateTokenCommand(
                    process.Id,
                    startId,
                    Array.Empty<Guid>()), txCt);

                if (!createResult.Success)
                {
                    _logger.LogWarning(
                        "[PROCESS-STARTED] Failed to create start token. ProcessId={ProcessId} StartId={StartId} Error={Error}",
                        process.Id, startId, createResult.Error);
                    continue;
                }

                created++;
                createdStartTokens.Add((createResult.TokenId, startId));
            }

            _logger.LogInformation(
                "[PROCESS-STARTED] Setup done. ProcessId={ProcessId} CreatedStartTokens={Created}",
                process.Id, created);

        }, ct);

        // ---- Record execution for created start tokens (best-effort, after commit) ----
        if (createdStartTokens.Count == 0)
            return;

    }
}
