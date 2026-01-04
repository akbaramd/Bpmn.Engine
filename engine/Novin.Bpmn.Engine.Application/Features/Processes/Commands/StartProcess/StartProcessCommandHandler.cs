using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.DomainServices;
using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Application.Commands.StartProcess;

public sealed class StartProcessCommandHandler : IRequestHandler<StartProcessCommand, StartProcessResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProcessInstantiationService _instantiator;
    private readonly ILogger<StartProcessCommandHandler> _logger;

    public StartProcessCommandHandler(
        IUnitOfWork unitOfWork,
        IProcessInstantiationService instantiator,
        ILogger<StartProcessCommandHandler> logger)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _instantiator = instantiator ?? throw new ArgumentNullException(nameof(instantiator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<StartProcessResult> Handle(StartProcessCommand request, CancellationToken cancellationToken)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
      
            // ------------------------------------------------------------
            // 2) Instantiate + start (domain service does BPMN checks + token creation)
            // ------------------------------------------------------------
            ValidateNewStart(request);

            var deployment = await _unitOfWork.Deployments.GetLatestByDeploymentKeyAsync(request.DeploymentKey, cancellationToken);
            if (deployment == null) throw new ArgumentNullException(nameof(deployment));

            var result = _instantiator.Instantiate(new ProcessInstantiationRequest(
                ProjectId: request.ProjectId,                 // ✅ ensure StartProcessCommand has ProjectId
                DeploymentId: deployment.Id,
                ProcessBpmnId: request.ProcessBpmnId!,
                ProcessName: request.ProcessName!,
                BusinessKey: request.BusinessKey,
                InitialVariables: request.InitialVariables ,
                ExplicitStartElementId: request.ExplicitStartElementId // optional
            ));

            // Persist created aggregates (Process + Token) within same transaction
            await _unitOfWork.Processes.AddAsync(result.Process, cancellationToken);
            await _unitOfWork.Tokens.AddAsync(result.InitialToken, cancellationToken);

            // Commit => outbox dispatches ProcessInstanceCreated/Started + TokenCreated/Activated
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            _logger.LogInformation(
                "Process instantiated. ProcessId={ProcessId}, DeploymentId={DeploymentId}, ProcessBpmnId={ProcessBpmnId}, Start={StartElementId}, TokenId={TokenId}.",
                result.Process.Id, result.Process.DeploymentId, result.Process.ProcessBpmnId, result.StartElementId, result.InitialToken.Id);

            return new StartProcessResult
            {
                ProcessId = result.Process.Id,
                ProcessName = result.Process.Name,
                CreatedAt = result.Process.CreatedAtUtc,
                StartedAt = result.Process.StartedAtUtc!.Value
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error starting process. ProcessBpmnId={ProcessBpmnId}",
                request.ProcessBpmnId);

            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    private static void ValidateNewStart(StartProcessCommand request)
    {
        if (request.ProjectId == Guid.Empty)
            throw new ArgumentException("ProjectId is required when creating a new process instance.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.DeploymentKey))
            throw new ArgumentException("DeploymentId is required when ProcessId is not provided.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.ProcessBpmnId))
            throw new ArgumentException("ProcessBpmnId is required when ProcessId is not provided.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.ProcessName))
            throw new ArgumentException("ProcessName is required when ProcessId is not provided.", nameof(request));
    }
}
