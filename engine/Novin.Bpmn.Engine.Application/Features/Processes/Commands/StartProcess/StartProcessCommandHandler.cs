using System.Linq;
using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.Commands.StartProcess;

public class StartProcessCommandHandler : IRequestHandler<StartProcessCommand, StartProcessResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<StartProcessCommandHandler> _logger;

    public StartProcessCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<StartProcessCommandHandler> logger)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<StartProcessResult> Handle(StartProcessCommand request, CancellationToken cancellationToken)
    {
        // Validate request
        if (!request.ProcessId.HasValue || request.ProcessId.Value == Guid.Empty)
        {
            if (request.DeploymentId == Guid.Empty)
            {
                throw new ArgumentException("DeploymentId is required when ProcessId is not provided.", nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.ProcessBpmnId))
            {
                throw new ArgumentException("ProcessBpmnId is required when ProcessId is not provided.", nameof(request));
            }

            if (string.IsNullOrWhiteSpace(request.ProcessName))
            {
                throw new ArgumentException("ProcessName is required when ProcessId is not provided.", nameof(request));
            }

            _logger.LogInformation("Starting process: {ProcessBpmnId} from deployment {DeploymentId} with name: {ProcessName}",
                request.ProcessBpmnId, request.DeploymentId, request.ProcessName);
        }
        else
        {
            _logger.LogInformation("Starting process instance: {ProcessId}", request.ProcessId);
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            if (request.ProcessId.HasValue && request.ProcessId.Value != Guid.Empty)
            {
                _logger.LogInformation("Starting existing process instance {ProcessId}", request.ProcessId);

                var existing = await _unitOfWork.Processes.GetByIdAsync(request.ProcessId.Value, cancellationToken);
                if (existing is null)
                    throw new InvalidOperationException($"Process with ID '{request.ProcessId}' not found.");

                existing.Start();

                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                return new StartProcessResult
                {
                    ProcessId = existing.Id,
                    ProcessName = existing.Name,
                    CreatedAt = existing.CreatedAt,
                    StartedAt = existing.StartedAt!.Value
                };
            }

            // Get deployment by ID
            var deployment = await _unitOfWork.Deployments.GetByIdAsync(request.DeploymentId, cancellationToken);

            if (deployment == null)
            {
                throw new InvalidOperationException(
                    $"Deployment with ID '{request.DeploymentId}' not found.");
            }

            if (!deployment.IsActive)
            {
                throw new InvalidOperationException(
                    $"Deployment '{request.DeploymentId}' is not active.");
            }

            // Parse BPMN definitions
            var bpmnDefinitions = deployment.GetDefinitions();
            if (bpmnDefinitions == null)
            {
                throw new InvalidOperationException(
                    $"Failed to parse BPMN definitions from deployment '{request.DeploymentId}'.");
            }

            var definitionsService = new BpmnDefinitionsService(bpmnDefinitions);

            // Get the specific process from BPMN definitions by ProcessBpmnId
            BpmnProcess? bpmnProcess;
            try
            {
                bpmnProcess = definitionsService.GetProcess(request.ProcessBpmnId);
            }
            catch (InvalidOperationException)
            {
                // GetProcess throws exception if not found, convert to our exception
                throw new InvalidOperationException(
                    $"Process '{request.ProcessBpmnId}' not found in deployment '{request.DeploymentId}'.");
            }

            if (bpmnProcess == null)
            {
                throw new InvalidOperationException(
                    $"Process '{request.ProcessBpmnId}' not found in deployment '{request.DeploymentId}'.");
            }

            var initialVariables = request.InitialVariables?
                .ToDictionary(kv => kv.Key, kv => (object?)kv.Value);

            var process = Process.Create(
                request.ProcessName,
                request.DeploymentId,
                request.ProcessBpmnId,
                initialVariables,
                request.BusinessKey);
            
            await _unitOfWork.Processes.AddAsync(process, cancellationToken);
         
            // Verify that start events exist (but don't create tokens here - ProcessStartedEventHandler will handle that)
            var startEvents = definitionsService.GetStartEvents(request.ProcessBpmnId);

            if (startEvents == null || startEvents.Count == 0)
            {
                throw new InvalidOperationException(
                    $"No start event found in process '{request.ProcessBpmnId}'.");
            }

            _logger.LogInformation("Found {StartEventCount} start events in process '{ProcessBpmnId}'.",
                startEvents.Count, request.ProcessBpmnId);

            // Start the process (this will raise ProcessStartedEvent, which ProcessStartedEventHandler will handle)
            process.Start();
            
            // Commit transaction (this will dispatch all events)
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            _logger.LogInformation("Process created and started. ProcessId: {ProcessId}, DeploymentId: {DeploymentId}, ProcessBpmnId: {ProcessBpmnId}, StartEvents: {StartEventCount}. Token creation will be handled by ProcessStartedEventHandler.",
                process.Id, process.DeploymentId, process.ProcessBpmnId, startEvents.Count);

            return new StartProcessResult
            {
                ProcessId = process.Id,
                ProcessName = process.Name,
                CreatedAt = process.CreatedAt,
                StartedAt = process.StartedAt!.Value
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting process: {ProcessBpmnId} from deployment {DeploymentId}", request.ProcessBpmnId, request.DeploymentId);
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}

