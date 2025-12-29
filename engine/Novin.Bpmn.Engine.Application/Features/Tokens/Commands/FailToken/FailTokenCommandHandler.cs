using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.Commands.FailToken;

public class FailTokenCommandHandler : IRequestHandler<FailTokenCommand, FailTokenResult>
{
    private readonly IUnitOfWork _uow;
    private readonly IIncidentService _incidentService;
    private readonly ILogger<FailTokenCommandHandler> _logger;

    public FailTokenCommandHandler(
        IUnitOfWork uow,
        IIncidentService incidentService,
        ILogger<FailTokenCommandHandler> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _incidentService = incidentService ?? throw new ArgumentNullException(nameof(incidentService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<FailTokenResult> Handle(FailTokenCommand request, CancellationToken ct)
    {
        _logger.LogInformation(
            "[FAIL-TOKEN] Failing token. ProcessId={ProcessId} TokenId={TokenId} ErrorType={ErrorType} ErrorCode={ErrorCode} ErrorMessage={ErrorMessage}",
            request.ProcessId,
            request.TokenId,
            request.ErrorType,
            request.ErrorCode,
            request.ErrorMessage);

        Guid? incidentId = null;

        await _uow.BeginTransactionAsync(ct);

        try
        {
            var token = await _uow.Tokens.GetByIdAsync(request.TokenId, ct);
            if (token == null)
            {
                _logger.LogWarning("[FAIL-TOKEN] Token not found. TokenId={TokenId}", request.TokenId);
                await _uow.RollbackTransactionAsync(ct);
                return new FailTokenResult(request.TokenId, false, null, "Token not found");
            }

            // Create incident if this is a technical failure or BPMN error
            if (request.ErrorType == "TechnicalFailure" || request.ErrorType == "BpmnError")
            {
                Incident incident;
                if (request.ErrorType == "TechnicalFailure")
                {
                    incident = await _incidentService.CreateTechnicalFailureAsync(
                        request.ProcessId,
                        request.TokenId,
                        token.CurrentElementId,
                        request.ErrorMessage,
                        $"Failed via command: {request.ErrorMessage}",
                        ct);
                }
                else // Must be "BpmnError" based on outer condition
                {
                    incident = await _incidentService.CreateBpmnErrorAsync(
                        request.ProcessId,
                        request.TokenId,
                        token.CurrentElementId,
                        request.ErrorCode ?? "Unknown",
                        request.ErrorMessage,
                        ct);
                }

                incidentId = incident.Id;
            }

            // Fail the token (this publishes TokenFailedEvent)
            var errorType = Enum.Parse<ErrorType>(request.ErrorType);
            token.Fail(request.ErrorMessage, errorType, request.ErrorCode, incidentId);

            await _uow.CommitTransactionAsync(ct);

            _logger.LogInformation(
                "[FAIL-TOKEN] Token failed successfully. ProcessId={ProcessId} TokenId={TokenId} IncidentId={IncidentId}",
                request.ProcessId,
                request.TokenId,
                incidentId);

            return new FailTokenResult(request.TokenId, true, incidentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FAIL-TOKEN] Error failing token. ProcessId={ProcessId} TokenId={TokenId}",
                request.ProcessId, request.TokenId);
            await _uow.RollbackTransactionAsync(ct);
            throw;
        }
    }
}