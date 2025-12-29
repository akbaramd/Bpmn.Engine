using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;

namespace Novin.Bpmn.Engine.Application.Commands.SuspendProcess;

public sealed class SuspendProcessCommandHandler : IRequestHandler<SuspendProcessCommand, SuspendProcessResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SuspendProcessCommandHandler> _logger;

    public SuspendProcessCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<SuspendProcessCommandHandler> logger)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SuspendProcessResult> Handle(SuspendProcessCommand request, CancellationToken cancellationToken)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var process = await _unitOfWork.Processes.GetByIdAsync(request.ProcessId, cancellationToken)
                          ?? throw new InvalidOperationException($"Process {request.ProcessId} not found.");

            process.Suspend(request.Reason);

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return new SuspendProcessResult
            {
                ProcessId = process.Id,
                SuspendedAt = process.SuspendedAt!.Value,
                Reason = request.Reason
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to suspend process {ProcessId}", request.ProcessId);
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}

