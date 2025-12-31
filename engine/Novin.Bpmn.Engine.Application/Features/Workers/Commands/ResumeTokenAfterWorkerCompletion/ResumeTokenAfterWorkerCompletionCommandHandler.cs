using MediatR;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Application.Commands;

public class ResumeTokenAfterWorkerCompletionCommandHandler
    : IRequestHandler<ResumeTokenAfterWorkerCompletionCommand>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWorkerRepository _workerRepository;
    private readonly ILogger<ResumeTokenAfterWorkerCompletionCommandHandler> _logger;

    public ResumeTokenAfterWorkerCompletionCommandHandler(
        IUnitOfWork unitOfWork,
        IWorkerRepository workerRepository,
        ILogger<ResumeTokenAfterWorkerCompletionCommandHandler> logger)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _workerRepository = workerRepository ?? throw new ArgumentNullException(nameof(workerRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(ResumeTokenAfterWorkerCompletionCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Resuming token after worker {WorkerId} completion", request.WorkerId);

        await _unitOfWork.ExecuteInTransactionAsync(async trxCt =>
        {
            // Get the worker
            var worker = await _workerRepository.GetByIdAsync(request.WorkerId, trxCt);
            if (worker == null)
            {
                _logger.LogWarning("Job {WorkerId} not found", request.WorkerId);
                return;
            }

            // Get the token
            var token = await _unitOfWork.Tokens.GetByIdAsync(worker.TokenId, trxCt);
            if (token == null)
            {
                _logger.LogError("Token {TokenId} not found for worker {WorkerId}",
                    worker.TokenId, request.WorkerId);
                return;
            }

            // Get the process for variable mapping
            var process = await _unitOfWork.Processes.GetByIdAsync(worker.ProcessId, trxCt);
            if (process == null)
            {
                _logger.LogError("Process {ProcessId} not found for worker {WorkerId}",
                    worker.ProcessId, request.WorkerId);
                return;
            }

            // Set result variables on token only (service task outputs are token-level)
            // ⛔ Output mapping is handled by ServiceTaskHandler.NodeProcessAsync in Resume path
            // (not here in event handler - mapping only happens at "activity execution boundary")
            if (request.Result != null)
            {
                // Set result variables on the token for downstream access (with overwriting)
                foreach (var kvp in request.Result)
                {
                    token.SetVariable(kvp.Key, kvp.Value);
                }

                _logger.LogInformation("Set {VariableCount} result variables on token {TokenId}",
                    request.Result.Count, worker.TokenId);
            }

            // Resume token processing (this will continue the BPMN flow)
            token.Resume();

            _logger.LogInformation("Job {WorkerId} completion processed, token {TokenId} resumed",
                request.WorkerId, worker.TokenId);
        }, cancellationToken);
    }
}