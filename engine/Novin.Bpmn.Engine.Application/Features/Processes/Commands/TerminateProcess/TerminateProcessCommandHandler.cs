using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;

namespace Novin.Bpmn.Engine.Application.Commands.TerminateProcess;

public sealed class TerminateProcessCommandHandler : IRequestHandler<TerminateProcessCommand, TerminateProcessResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TerminateProcessCommandHandler> _logger;

    public TerminateProcessCommandHandler(
        IUnitOfWork unitOfWork,
        ILogger<TerminateProcessCommandHandler> logger)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TerminateProcessResult> Handle(TerminateProcessCommand request, CancellationToken cancellationToken)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var process = await _unitOfWork.Processes.GetByIdAsync(request.ProcessId, cancellationToken)
                          ?? throw new InvalidOperationException($"Process {request.ProcessId} not found.");

            process.Terminate(request.Reason);

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return new TerminateProcessResult
            {
                ProcessId = process.Id,
                TerminatedAt = process.TerminatedAt!.Value,
                Reason = request.Reason
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to terminate process {ProcessId}", request.ProcessId);
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}

