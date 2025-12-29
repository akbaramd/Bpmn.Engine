using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Application.Commands.CreateProcessInstance;

public sealed class CreateProcessInstanceCommandHandler : IRequestHandler<CreateProcessInstanceCommand, CreateProcessInstanceResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateProcessInstanceCommandHandler> _logger;

    public CreateProcessInstanceCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<CreateProcessInstanceCommandHandler> logger)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CreateProcessInstanceResult> Handle(CreateProcessInstanceCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating process instance {ProcessName} for process {ProcessBpmnId} in deployment {DeploymentId}",
            request.ProcessName, request.ProcessBpmnId, request.DeploymentId);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var deployment = await _unitOfWork.Deployments.GetByIdAsync(request.DeploymentId, cancellationToken)
                             ?? throw new InvalidOperationException($"Deployment with ID '{request.DeploymentId}' not found.");

            if (!deployment.IsActive)
                throw new InvalidOperationException($"Deployment '{request.DeploymentId}' is not active.");

            var bpmnDefinitions = deployment.GetDefinitions();
            var definitionsService = new BpmnDefinitionsService(bpmnDefinitions);

            var bpmnProcess = definitionsService.GetProcess(request.ProcessBpmnId)
                             ?? throw new InvalidOperationException($"Process '{request.ProcessBpmnId}' not found in deployment '{request.DeploymentId}'.");

            var startEvents = definitionsService.GetStartEvents(request.ProcessBpmnId);
            if (startEvents.Count == 0)
                throw new InvalidOperationException($"No start event found in process '{request.ProcessBpmnId}'.");

            var process = Process.Create(
                request.ProcessName,
                request.DeploymentId,
                request.ProcessBpmnId,
                request.InitialVariables,
                request.BusinessKey);

            await _unitOfWork.Processes.AddAsync(process, cancellationToken);

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            _logger.LogInformation("Process instance created: {ProcessId}", process.Id);

            return new CreateProcessInstanceResult
            {
                ProcessId = process.Id,
                ProcessName = process.Name,
                CreatedAt = process.CreatedAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating process instance for {ProcessBpmnId} in deployment {DeploymentId}",
                request.ProcessBpmnId, request.DeploymentId);
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}

