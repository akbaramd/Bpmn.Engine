using System.Linq;
using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.Commands.SetProcessVariables;

public sealed class SetProcessVariablesCommandHandler : IRequestHandler<SetProcessVariablesCommand, SetProcessVariablesResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SetProcessVariablesCommandHandler> _logger;

    public SetProcessVariablesCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<SetProcessVariablesCommandHandler> logger)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SetProcessVariablesResult> Handle(SetProcessVariablesCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Applying variables patch to process {ProcessId}", request.ProcessId);

        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var process = await _unitOfWork.Processes.GetByIdAsync(request.ProcessId, cancellationToken)
                          ?? throw new InvalidOperationException($"Process {request.ProcessId} not found.");

            var patch = ProcessVariablesPatch.From(request.Upserts, request.Removals);
            process.ApplyVariablesPatch(patch);

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return new SetProcessVariablesResult
            {
                ProcessId = process.Id,
                UpsertedKeys = patch.Upserts.Keys.ToArray(),
                RemovedKeys = patch.Removals.ToArray()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply variables patch to process {ProcessId}", request.ProcessId);
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}

