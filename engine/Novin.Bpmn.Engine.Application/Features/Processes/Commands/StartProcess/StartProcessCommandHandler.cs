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
            // 1) Start an existing process instance (rare path)
            // ------------------------------------------------------------
            if (request.ProcessId.HasValue && request.ProcessId.Value != Guid.Empty)
            {
                var existing = await _unitOfWork.Processes.GetByIdAsync(request.ProcessId.Value, cancellationToken);
                if (existing is null)
                    throw new InvalidOperationException($"Process with ID '{request.ProcessId}' not found.");

                existing.Start();

                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                return new StartProcessResult
                {
                    ProcessId = existing.Id,
                    ProcessName = existing.Name,
                    CreatedAt = existing.CreatedAtUtc,
                    StartedAt = existing.StartedAtUtc!.Value
                };
            }

            // ------------------------------------------------------------
            // 2) Instantiate + start (domain service does BPMN checks + token creation)
            // ------------------------------------------------------------
            ValidateNewStart(request);

            var initialVariables = request.InitialVariables?
                .ToDictionary(kv => kv.Key, kv => (object?)kv.Value);

            var result = _instantiator.Instantiate(new ProcessInstantiationRequest(
                ProjectId: request.ProjectId,                 // ✅ ensure StartProcessCommand has ProjectId
                DeploymentId: request.DeploymentId,
                ProcessBpmnId: request.ProcessBpmnId!,
                ProcessName: request.ProcessName!,
                BusinessKey: request.BusinessKey,
                InitialVariables: initialVariables,
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
                "Error starting process. ProcessBpmnId={ProcessBpmnId}, DeploymentId={DeploymentId}",
                request.ProcessBpmnId, request.DeploymentId);

            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    private static void ValidateNewStart(StartProcessCommand request)
    {
        if (request.ProjectId == Guid.Empty)
            throw new ArgumentException("ProjectId is required when creating a new process instance.", nameof(request));

        if (request.DeploymentId == Guid.Empty)
            throw new ArgumentException("DeploymentId is required when ProcessId is not provided.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.ProcessBpmnId))
            throw new ArgumentException("ProcessBpmnId is required when ProcessId is not provided.", nameof(request));

        if (string.IsNullOrWhiteSpace(request.ProcessName))
            throw new ArgumentException("ProcessName is required when ProcessId is not provided.", nameof(request));
    }
}
