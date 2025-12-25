using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.Events;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

public sealed class ProcessStartedEventHandler : INotificationHandler<ProcessStartedEvent>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<ProcessStartedEventHandler> _logger;

    public ProcessStartedEventHandler(
        IUnitOfWork uow,
        ILogger<ProcessStartedEventHandler> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task Handle(ProcessStartedEvent @event, CancellationToken ct)
        => _uow.ExecuteInTransactionAsync(async txCt =>
        {
            _logger.LogInformation(
                "[PROCESS-STARTED] Handling ProcessStartedEvent. ProcessId={ProcessId} EventStartedAt={StartedAt}",
                @event.ProcessId,
                @event.StartedAt);

            var process = await _uow.Processes.GetByIdAsync(@event.ProcessId, txCt);
            if (process is null)
            {
                _logger.LogWarning("Process not found. ProcessId={ProcessId}", @event.ProcessId);
                return;
            }

            // ⚠️ Guard: اگر پروسس قبلاً Completed/Terminated/Failed شده، توکن start ایجاد نکن
            if (process.State is not ProcessState.Running and not ProcessState.Created)
            {
                _logger.LogWarning(
                    "[PROCESS-STARTED] ⚠️ Process not in Running/Created state. Skipping token creation. ProcessId={ProcessId} State={State} StartedAt={StartedAt} CompletedAt={CompletedAt}",
                    @event.ProcessId,
                    process.State,
                    process.StartedAt,
                    process.CompletedAt);
                return;
            }

            _logger.LogDebug(
                "[PROCESS-STARTED] Process is in valid state. ProcessId={ProcessId} State={State}",
                @event.ProcessId,
                process.State);


            var deployment = await _uow.Deployments
                .GetLatestByDeploymentKeyAsync(process.ProcessDefinitionId, txCt);

            if (deployment is null)
            {
                _logger.LogWarning(
                    "Deployment not found. ProcessDefinitionId={ProcessDefinitionId}",
                    process.ProcessDefinitionId);
                return;
            }

            var defs = deployment.GetDefinitions();
            var defsService = new BpmnDefinitionsService(defs);

            var firstProc = defsService.GetFirstProcess();
            var bpmnProcessId = firstProc?.id ?? process.ProcessDefinitionId;

            var startEvents = defsService.GetStartEvents(bpmnProcessId)
                .Where(se => !string.IsNullOrWhiteSpace(se.id))
                .ToList();

            if (startEvents.Count == 0)
            {
                _logger.LogWarning("No start events found. BpmnProcessId={BpmnProcessId}", bpmnProcessId);
                return;
            }

            // یک بار کل توکن‌های این پروسس را بگیر
            var allTokens = await _uow.Tokens.GetByProcessIdAsync(process.Id, txCt);

            // توکن‌هایی که هنوز زنده‌اند (برای idempotency)
            var alive = allTokens
                .Where(t => t.State is not TokenState.Completed
                         && t.State is not TokenState.Terminated
                         && t.State is not TokenState.Failed)
                .GroupBy(t => t.CurrentElementId)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

            var created = 0;

            foreach (var se in startEvents)
            {
                var startId = se.id!;

                // اگر قبلاً توکن زنده روی همین start داریم، دوباره نساز
                if (alive.TryGetValue(startId, out var existingAtStart) && existingAtStart.Count > 0)
                {
                    _logger.LogInformation(
                        "Start token already exists. ProcessId={ProcessId}, StartId={StartId}, Count={Count}",
                        process.Id, startId, existingAtStart.Count);
                    continue;
                }

                var token = new Token(process.Id, startId, parentTokenIds: Array.Empty<Guid>());

                // Add to DbContext
                await _uow.Tokens.AddAsync(token, txCt);

                // keep relation in aggregate
                process.AddToken(token.Id);

                // فعال کن تا TokenProcessingRequestedEvent تولید شود
                token.Activate();

                created++;
            }

            _logger.LogInformation(
                "ProcessStartedEvent setup done. ProcessId={ProcessId}, CreatedStartTokens={Created}",
                process.Id, created);

            // Commit توسط ExecuteInTransactionAsync انجام می‌شود
        }, ct);
}
