using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.Services;

public sealed class TokenManagementService : ITokenManagementService
{
    private readonly IUnitOfWork _uow;
    private readonly ITransactionService _transactionService;
    private readonly IIncidentService _incidentService;
    private readonly IProcessCompletionEvaluator _completionEvaluator;
    private readonly ILogger<TokenManagementService> _logger;

    public TokenManagementService(
        IUnitOfWork uow,
        ITransactionService transactionService,
        IIncidentService incidentService,
        IProcessCompletionEvaluator completionEvaluator,
        ILogger<TokenManagementService> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _transactionService = transactionService ?? throw new ArgumentNullException(nameof(transactionService));
        _incidentService = incidentService ?? throw new ArgumentNullException(nameof(incidentService));
        _completionEvaluator = completionEvaluator ?? throw new ArgumentNullException(nameof(completionEvaluator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task RetryTokenAsync(Guid tokenId, CancellationToken ct = default)
    {
        await _transactionService.ExecuteInTransactionAsync(async trxCt =>
        {
            var token = await _uow.Tokens.GetByIdAsync(tokenId, trxCt);
            if (token == null)
            {
                _logger.LogWarning("[TOKEN_MGMT] Token not found for retry. TokenId={TokenId}", tokenId);
                return;
            }

            if (token.State != TokenState.Failed)
            {
                _logger.LogWarning(
                    "[TOKEN_MGMT] Token not in Failed state. Cannot retry. TokenId={TokenId} State={State}",
                    tokenId,
                    token.State);
                return;
            }

            // Retry the associated incident if exists
            var incidents = await _uow.Incidents.GetByTokenIdAsync(tokenId, trxCt);
            var openIncident = incidents.FirstOrDefault(i => i.Status == Domain.ValueObjects.IncidentStatus.Open);
            if (openIncident != null)
            {
                openIncident.Retry();
                await _uow.Incidents.UpdateAsync(openIncident, trxCt);
                _logger.LogInformation(
                    "[TOKEN_MGMT] Incident retried. IncidentId={IncidentId} Retries={Retries}",
                    openIncident.Id,
                    openIncident.Retries);
            }

            // Retry the token (converts Failed -> Active and requests processing)
            token.Retry();

            // SaveChanges is handled by TransactionService automatically

            _logger.LogInformation(
                "[TOKEN_MGMT] Token retried successfully. TokenId={TokenId} ProcessId={ProcessId} ElementId={ElementId}",
                tokenId,
                token.ProcessId,
                token.CurrentElementId);
        }, ct);

        // Evaluate process completion after retry
        var token = await _uow.Tokens.GetByIdAsync(tokenId, ct);
        if (token != null)
        {
            await _completionEvaluator.EvaluateCompletionAsync(token.ProcessId, ct);
        }
    }

    public async Task MoveTokenAsync(Guid tokenId, string targetElementId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(targetElementId))
            throw new ArgumentException("Target element id cannot be null or empty", nameof(targetElementId));

        await _transactionService.ExecuteInTransactionAsync(async trxCt =>
        {
            var token = await _uow.Tokens.GetByIdAsync(tokenId, trxCt);
            if (token == null)
            {
                _logger.LogWarning("[TOKEN_MGMT] Token not found for move. TokenId={TokenId}", tokenId);
                return;
            }

            // Token must be Active or Failed to move
            if (token.State != TokenState.Active && token.State != TokenState.Failed)
            {
                _logger.LogWarning(
                    "[TOKEN_MGMT] Token not in Active or Failed state. Cannot move. TokenId={TokenId} State={State}",
                    tokenId,
                    token.State);
                return;
            }

            // If token is Failed, retry it first (convert to Active)
            if (token.State == TokenState.Failed)
            {
                token.Retry();
                _logger.LogInformation(
                    "[TOKEN_MGMT] Token retried before move. TokenId={TokenId}",
                    tokenId);
            }

            // Move token to target element
            token.MoveTo(targetElementId, viaFlowId: null);

            // SaveChanges is handled by TransactionService automatically

            _logger.LogInformation(
                "[TOKEN_MGMT] Token moved successfully. TokenId={TokenId} ProcessId={ProcessId} From={FromElementId} To={ToElementId}",
                tokenId,
                token.ProcessId,
                token.CurrentElementId,
                targetElementId);
        }, ct);
    }

    public async Task TerminateTokenAsync(Guid tokenId, string? reason = null, CancellationToken ct = default)
    {
        await _transactionService.ExecuteInTransactionAsync(async trxCt =>
        {
            var token = await _uow.Tokens.GetByIdAsync(tokenId, trxCt);
            if (token == null)
            {
                _logger.LogWarning("[TOKEN_MGMT] Token not found for terminate. TokenId={TokenId}", tokenId);
                return;
            }

            if (token.State == TokenState.Completed)
            {
                _logger.LogWarning(
                    "[TOKEN_MGMT] Cannot terminate completed token. TokenId={TokenId}",
                    tokenId);
                return;
            }

            var process = await _uow.Processes.GetByIdAsync(token.ProcessId, trxCt);
            if (process == null)
            {
                _logger.LogWarning(
                    "[TOKEN_MGMT] Process not found for token termination. ProcessId={ProcessId} TokenId={TokenId}",
                    token.ProcessId,
                    tokenId);
                return;
            }

            // Terminate the token
            token.Terminate(reason ?? "Manual termination via TokenManagementService");

            // Remove token from process
            process.RemoveToken(tokenId);

            // SaveChanges is handled by TransactionService automatically

            _logger.LogWarning(
                "[TOKEN_MGMT] Token terminated. TokenId={TokenId} ProcessId={ProcessId} ElementId={ElementId} Reason={Reason}",
                tokenId,
                token.ProcessId,
                token.CurrentElementId,
                reason ?? "Manual termination");
        }, ct);

        // Evaluate process completion after termination
        var token = await _uow.Tokens.GetByIdAsync(tokenId, ct);
        if (token != null)
        {
            await _completionEvaluator.EvaluateCompletionAsync(token.ProcessId, ct);
        }
    }
}

