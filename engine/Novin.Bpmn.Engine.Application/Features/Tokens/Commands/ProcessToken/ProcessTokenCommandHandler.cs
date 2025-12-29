using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Commands.FailToken;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.Exceptions;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.Commands.ProcessToken;

/// <summary>
/// Standard ProcessTokenCommandHandler according to BPMN execution model.
/// Responsibilities:
/// 1. Load Token + Process
/// 2. Validate state
/// 3. Build Runtime Context (BPMN model)
/// 4. Dispatch to appropriate ElementHandler
/// 
/// All Navigate/Route/Complete/Fork logic belongs in ElementHandlers, not here.
/// </summary>
public sealed class ProcessTokenCommandHandler : IRequestHandler<ProcessTokenCommand, ProcessTokenResult>
{
    private readonly IUnitOfWork _uow;
    private readonly IBpmnRuntimeContextFactory _ctxFactory;
    private readonly ITokenExecutionDispatcher _dispatcher;
    private readonly IMediator _mediator;
    private readonly ILogger<ProcessTokenCommandHandler> _logger;
    private readonly IProcessExecutionRecorder _executionRecorder;

    public ProcessTokenCommandHandler(
        IUnitOfWork uow,
        IBpmnRuntimeContextFactory ctxFactory,
        ITokenExecutionDispatcher dispatcher,
        IMediator mediator,
        ILogger<ProcessTokenCommandHandler> logger,
        IProcessExecutionRecorder executionRecorder)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _ctxFactory = ctxFactory ?? throw new ArgumentNullException(nameof(ctxFactory));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _executionRecorder = executionRecorder ?? throw new ArgumentNullException(nameof(executionRecorder));
    }

    public async Task<ProcessTokenResult> Handle(ProcessTokenCommand request, CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "[PROCESS-TOKEN] Processing token. ProcessId={ProcessId} TokenId={TokenId}",
            request.ProcessId, request.TokenId);

        try
        {
            await _uow.ExecuteInTransactionAsync(async trxCt =>
            {
                // 2.1 Load Token + Process
                var process = await _uow.Processes.GetByIdAsync(request.ProcessId, trxCt);
                if (process == null)
                {
                    _logger.LogWarning("[PROCESS-TOKEN] Process not found. ProcessId={ProcessId}", request.ProcessId);
                    return;
                }

                var token = await _uow.Tokens.GetByIdAsync(request.TokenId, trxCt);
                if (token == null || token.ProcessId != request.ProcessId)
        {
                    _logger.LogWarning("[PROCESS-TOKEN] Token not found or process mismatch. TokenId={TokenId} ProcessId={ProcessId}",
                        request.TokenId, request.ProcessId);
                    return;
                }

                // 2.1 Validate state
                if (token.State != TokenState.Active)
                {
                    _logger.LogDebug(
                        "[PROCESS-TOKEN] Token not in Active state. TokenId={TokenId} State={State}",
                        request.TokenId, token.State);
                    return; // Not an error - token is waiting/completed/failed/terminated
                }

                // 2.2 Build Runtime Context
                var ctx = await _ctxFactory.CreateAsync(process, trxCt);

                // Resolve element from BPMN model
                var element = ctx.Model.GetElementById(ctx.BpmnProcessId, token.CurrentElementId);
                if (element == null)
                {
                    throw new TokenExecutionException(
                        request.ProcessId,
                        request.TokenId,
                        token.CurrentElementId,
                        $"Element '{token.CurrentElementId}' not found in BPMN model.");
        }

                // 2.3 Dispatch to ElementHandler
                using (_logger.BeginScope(new Dictionary<string, string?>
                       {
                           ["ProcessId"] = process.Id.ToString(),
                           ["TokenId"] = token.Id.ToString(),
                           ["ElementId"] = token.CurrentElementId,
                           ["ScopeId"] = token.ScopeId?.ToString(),
                           ["ArrivedVia"] = token.ArrivedViaFlowId,
                           ["Executable"] = token.IsExecutable.ToString(),
                           ["State"] = token.State.ToString(),
                           ["IsResume"] = request.IsResume.ToString()
                       }))
                {
                    await _dispatcher.DispatchProcessAsync(process, token, element, ctx, request.IsResume, trxCt);
                }
            }, cancellationToken);

            _logger.LogInformation("[PROCESS-TOKEN] Processing completed. TokenId={TokenId}", request.TokenId);
            return new ProcessTokenResult(request.TokenId, true);
        }
        catch (BpmnErrorException bex)
        {
            _logger.LogWarning(
                "[PROCESS-TOKEN] BPMN Error caught. ProcessId={ProcessId} TokenId={TokenId} ErrorCode={ErrorCode} Message={Message}",
                request.ProcessId, request.TokenId, bex.Code, bex.Message);

            // Report BPMN error through aggregate behavior (publishes BpmnErrorOccurredEvent)
            await _uow.ExecuteInTransactionAsync(async trxCt =>
            {
                var token = await _uow.Tokens.GetByIdAsync(request.TokenId, trxCt);
                if (token != null)
                {
                    token.ReportBpmnError(bex.Code, bex.Message);
                }
            }, cancellationToken);

            return new ProcessTokenResult(request.TokenId, false, $"BPMN Error: {bex.Message}");
        }
        catch (TokenExecutionException tex)
        {
            _logger.LogError(tex,
                "[PROCESS-TOKEN] Technical failure. ProcessId={ProcessId} TokenId={TokenId}",
                request.ProcessId, request.TokenId);

            // Report technical failure through aggregate behavior
            await _uow.ExecuteInTransactionAsync(async trxCt =>
            {
                var token = await _uow.Tokens.GetByIdAsync(request.TokenId, trxCt);
                if (token != null)
                {
                    var stackTrace = tex.InnerException?.ToString() ?? tex.StackTrace ?? string.Empty;
                    token.ReportTechnicalFailure(tex.Message, stackTrace);
                }
            }, cancellationToken);

            return new ProcessTokenResult(request.TokenId, false, $"Technical failure: {tex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[PROCESS-TOKEN] ⚠️ Unexpected error processing token. TokenId={TokenId} ProcessId={ProcessId}",
                request.TokenId, request.ProcessId);

            // Fail the token via command (creates incident and publishes TokenFailedEvent)
            try
            {
                var failCommand = new FailTokenCommand(
                    ProcessId: request.ProcessId,
                    TokenId: request.TokenId,
                    ErrorMessage: $"Processing failed: {ex.Message}",
                    ErrorType: "TechnicalFailure",
                    ErrorCode: null);

                await _mediator.Send(failCommand, cancellationToken);

                _logger.LogInformation("[PROCESS-TOKEN] Token marked as failed. TokenId={TokenId}", request.TokenId);
            }
            catch (Exception failEx)
            {
                _logger.LogError(failEx, "[PROCESS-TOKEN] Failed to mark token as failed. TokenId={TokenId}", request.TokenId);
            }

            return new ProcessTokenResult(request.TokenId, false, $"Processing failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Get element type string from BPMN element
    /// </summary>
    private string GetElementType(BpmnFlowElement element)
    {
        if (element == null) return "Unknown";

        return element.GetType().Name switch
        {
            "BpmnStartEvent" => "StartEvent",
            "BpmnEndEvent" => "EndEvent",
            "BpmnUserTask" => "UserTask",
            "BpmnScriptTask" => "ScriptTask",
            "BpmnServiceTask" => "ServiceTask",
            "BpmnBoundaryEvent" => "BoundaryEvent",
            "BpmnIntermediateCatchEvent" => "IntermediateCatchEvent",
            "BpmnIntermediateThrowEvent" => "IntermediateThrowEvent",
            "BpmnExclusiveGateway" => "ExclusiveGateway",
            "BpmnParallelGateway" => "ParallelGateway",
            "BpmnInclusiveGateway" => "InclusiveGateway",
            "BpmnEventBasedGateway" => "EventBasedGateway",
            _ => element.GetType().Name
        };
    }

    /// <summary>
    /// Get element name from BPMN element
    /// </summary>
    private string? GetElementName(BpmnFlowElement element)
    {
        if (element == null) return null;

        // Try to get name property using reflection
        var nameProperty = element.GetType().GetProperty("name");
        return nameProperty?.GetValue(element) as string;
    }
}

