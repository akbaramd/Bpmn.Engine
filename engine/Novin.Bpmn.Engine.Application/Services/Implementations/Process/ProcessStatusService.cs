using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.Services;

public sealed class ProcessStatusService : IProcessStatusService
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<ProcessStatusService> _logger;

    public ProcessStatusService(
        IUnitOfWork uow,
        ILogger<ProcessStatusService> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ProcessDerivedStatus> GetDerivedStatusAsync(Process process, CancellationToken ct = default)
    {
        if (process == null)
            throw new ArgumentNullException(nameof(process));

        // Terminal states don't need derived status calculation
        switch (process.State)
        {
            case ProcessState.Created:
                return ProcessDerivedStatus.Created;
            case ProcessState.Completed:
                return ProcessDerivedStatus.Completed;
            case ProcessState.Terminated:
                return ProcessDerivedStatus.Terminated;
            case ProcessState.Failed:
                return ProcessDerivedStatus.Failed;
            case ProcessState.Suspended:
                return ProcessDerivedStatus.Suspended;
            case ProcessState.Running:
                // For Running state, check if there are open incidents
                // This indicates incident-driven execution (blocked but recoverable)
                var openIncidents = await _uow.Incidents.GetByProcessIdAsync(process.Id, ct);
                var hasOpenIncidents = openIncidents.Any(i => i.Status == IncidentStatus.Open);

                if (hasOpenIncidents)
                {
                    _logger.LogDebug(
                        "[PROCESS_STATUS] Process has open incidents. DerivedStatus=RunningWithIncidents. ProcessId={ProcessId}",
                        process.Id);
                    return ProcessDerivedStatus.RunningWithIncidents;
                }

                return ProcessDerivedStatus.Running;
            default:
                _logger.LogWarning(
                    "[PROCESS_STATUS] Unknown ProcessState. Defaulting to Running. ProcessId={ProcessId} State={State}",
                    process.Id,
                    process.State);
                return ProcessDerivedStatus.Running;
        }
    }
}

