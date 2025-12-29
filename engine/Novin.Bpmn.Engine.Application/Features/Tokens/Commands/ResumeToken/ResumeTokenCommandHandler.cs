using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.Commands.ResumeToken;

public sealed class ResumeTokenCommandHandler : IRequestHandler<ResumeTokenCommand, ResumeTokenResult>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<ResumeTokenCommandHandler> _logger;

    public ResumeTokenCommandHandler(IUnitOfWork uow, ILogger<ResumeTokenCommandHandler> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ResumeTokenResult> Handle(ResumeTokenCommand request, CancellationToken cancellationToken)
    {
        await _uow.BeginTransactionAsync(cancellationToken);
        try
        {
            var token = await _uow.Tokens.GetByIdAsync(request.TokenId, cancellationToken);
            if (token is null || token.ProcessId != request.ProcessId)
            {
                await _uow.RollbackTransactionAsync(cancellationToken);
                return new ResumeTokenResult(request.TokenId, false, "Token not found");
            }

            if (token.State != TokenState.Waiting)
            {
                await _uow.RollbackTransactionAsync(cancellationToken);
                return new ResumeTokenResult(request.TokenId, false, $"Token is {token.State}, expected Waiting");
            }

            token.Resume();

            await _uow.CommitTransactionAsync(cancellationToken);
            return new ResumeTokenResult(request.TokenId, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RESUME-TOKEN] Failed. TokenId={TokenId}", request.TokenId);
            await _uow.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}

