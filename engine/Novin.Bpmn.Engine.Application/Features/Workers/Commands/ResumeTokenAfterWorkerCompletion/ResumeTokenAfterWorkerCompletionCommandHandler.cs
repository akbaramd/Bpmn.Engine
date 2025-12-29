using MediatR;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Application.Commands;

public class ResumeTokenAfterWorkerCompletionCommandHandler
    : IRequestHandler<ResumeTokenAfterWorkerCompletionCommand>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IWorkerRepository _workerRepository;
    private readonly IBpmnRuntimeContextFactory _ctxFactory;
    private readonly IVariableMappingService _variableMapping;
    private readonly ILogger<ResumeTokenAfterWorkerCompletionCommandHandler> _logger;

    public ResumeTokenAfterWorkerCompletionCommandHandler(
        IUnitOfWork unitOfWork,
        IWorkerRepository workerRepository,
        IBpmnRuntimeContextFactory ctxFactory,
        IVariableMappingService variableMapping,
        ILogger<ResumeTokenAfterWorkerCompletionCommandHandler> logger)
    {
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _workerRepository = workerRepository ?? throw new ArgumentNullException(nameof(workerRepository));
        _ctxFactory = ctxFactory ?? throw new ArgumentNullException(nameof(ctxFactory));
        _variableMapping = variableMapping ?? throw new ArgumentNullException(nameof(variableMapping));
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
            // The decorator will handle moving them to process when appropriate
            if (request.Result != null)
            {
                // Set result variables on the token for downstream access (with overwriting)
                foreach (var kvp in request.Result)
                {
                    token.SetVariable(kvp.Key, kvp.Value);
                }

                _logger.LogInformation("Set {VariableCount} result variables on token {TokenId}",
                    request.Result.Count, worker.TokenId);

                // Apply output mapping to move variables from token to process
                // This should happen after the service task completes but before token resumes
                try
                {
                    // Create the runtime context for the process
                    var ctx = await _ctxFactory.CreateAsync(process, trxCt);

                    // Get the BPMN element for this service task
                    var element = ctx.Model?.GetElementById(ctx.BpmnProcessId, worker.ElementId);
                    if (element != null)
                    {
                        // Apply output mapping using the mapping service
                        // This moves variables from token to process according to BPMN ioMapping
                        _variableMapping.ApplyOutputs(process, token, element, ctx);

                        _logger.LogInformation("Applied output mapping for service task {ElementId} on process {ProcessId}",
                            worker.ElementId, worker.ProcessId);
                    }
                    else
                    {
                        _logger.LogWarning("Could not find BPMN element {ElementId} for output mapping", worker.ElementId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error applying output mapping for service task {ElementId}", worker.ElementId);
                    // Continue with token resumption even if mapping fails
                }
            }

            // Resume token processing (this will continue the BPMN flow)
            token.Resume();

            _logger.LogInformation("Job {WorkerId} completion processed, token {TokenId} resumed",
                request.WorkerId, worker.TokenId);
        }, cancellationToken);
    }
}