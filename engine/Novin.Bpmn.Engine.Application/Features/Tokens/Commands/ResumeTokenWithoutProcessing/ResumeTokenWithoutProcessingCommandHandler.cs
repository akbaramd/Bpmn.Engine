using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.Commands.ResumeTokenWithoutProcessing;

public sealed class ResumeTokenWithoutProcessingCommandHandler : IRequestHandler<ResumeTokenWithoutProcessingCommand, ResumeTokenWithoutProcessingResult>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<ResumeTokenWithoutProcessingCommandHandler> _logger;

    public ResumeTokenWithoutProcessingCommandHandler(IUnitOfWork uow, ILogger<ResumeTokenWithoutProcessingCommandHandler> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ResumeTokenWithoutProcessingResult> Handle(ResumeTokenWithoutProcessingCommand request, CancellationToken cancellationToken)
    {
        await _uow.BeginTransactionAsync(cancellationToken);
        try
        {
            var token = await _uow.Tokens.GetByIdAsync(request.TokenId, cancellationToken);
            if (token is null || token.ProcessId != request.ProcessId)
            {
                await _uow.RollbackTransactionAsync(cancellationToken);
                return new ResumeTokenWithoutProcessingResult(request.TokenId, false, "Token not found");
            }

            if (token.State != TokenState.Waiting)
            {
                await _uow.RollbackTransactionAsync(cancellationToken);
                return new ResumeTokenWithoutProcessingResult(request.TokenId, false, $"Token is {token.State}, expected Waiting");
            }

            token.ResumeWithoutProcessing();

            await _uow.CommitTransactionAsync(cancellationToken);
            return new ResumeTokenWithoutProcessingResult(request.TokenId, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RESUME-TOKEN-WO-PROC] Failed. TokenId={TokenId}", request.TokenId);
            await _uow.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}

