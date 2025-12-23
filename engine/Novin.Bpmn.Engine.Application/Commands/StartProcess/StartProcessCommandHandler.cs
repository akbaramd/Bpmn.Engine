using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Models.Models;
using Node = Novin.Bpmn.Engine.Domain.Entities.Node;

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
        _logger.LogInformation("Starting process: {ProcessDefinitionId} with name: {ProcessName}", 
            request.ProcessDefinitionId, request.ProcessName);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            // Get deployment by deployment key (ProcessDefinitionId)
            var deployment = await _unitOfWork.Deployments.GetLatestByDeploymentKeyAsync(
                request.ProcessDefinitionId, cancellationToken);
            
            if (deployment == null)
            {
                throw new InvalidOperationException(
                    $"Deployment with key '{request.ProcessDefinitionId}' not found.");
            }

            // Parse BPMN definitions
            var bpmnDefinitions = deployment.GetDefinitions();
            var definitionsService = new BpmnDefinitionsService(bpmnDefinitions);
            
            // Get the process from BPMN definitions
            var bpmnProcess = definitionsService.GetFirstProcess();
            var processId = bpmnProcess.id ?? request.ProcessDefinitionId;

            // Create process instance
            var process = new Process(request.ProcessName, request.ProcessDefinitionId, request.InitialVariables);
            
            await _unitOfWork.Processes.AddAsync(process, cancellationToken);
         
            // Create nodes from BPMN definitions
            var allFlowElements = definitionsService.GetAllFlowElements(processId);
            var createdNodes = new Dictionary<string, Guid>(); // elementId -> nodeId

      
            // Verify that start events exist (but don't create tokens here - ProcessStartedEventHandler will handle that)
            var startEvents = definitionsService.GetStartEvents(processId);
            
            if (startEvents.Count == 0)
            {
                throw new InvalidOperationException(
                    $"No start event found in process '{processId}'.");
            }

            _logger.LogInformation("Created {NodeCount} nodes for process. Found {StartEventCount} start events.", 
                createdNodes.Count, startEvents.Count);

            // Start the process (this will raise ProcessStartedEvent, which ProcessStartedEventHandler will handle)
            process.Start();
            
            
         
            
            // Commit transaction (this will dispatch all events)
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            _logger.LogInformation("Process created and started. ProcessId: {ProcessId}, Nodes: {NodeCount}. Token creation will be handled by ProcessStartedEventHandler.", 
                process.Id, createdNodes.Count);

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
            _logger.LogError(ex, "Error starting process: {ProcessDefinitionId}", request.ProcessDefinitionId);
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}

