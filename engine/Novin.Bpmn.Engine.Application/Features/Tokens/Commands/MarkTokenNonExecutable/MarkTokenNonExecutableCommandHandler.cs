using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;

namespace Novin.Bpmn.Engine.Application.Commands.MarkTokenNonExecutable;

public sealed class MarkTokenNonExecutableCommandHandler : IRequestHandler<MarkTokenNonExecutableCommand, MarkTokenNonExecutableResult>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<MarkTokenNonExecutableCommandHandler> _logger;

    public MarkTokenNonExecutableCommandHandler(IUnitOfWork uow, ILogger<MarkTokenNonExecutableCommandHandler> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<MarkTokenNonExecutableResult> Handle(MarkTokenNonExecutableCommand request, CancellationToken cancellationToken)
    {
        await _uow.BeginTransactionAsync(cancellationToken);
        try
        {
            var token = await _uow.Tokens.GetByIdAsync(request.TokenId, cancellationToken);
            if (token is null || token.ProcessId != request.ProcessId)
            {
                await _uow.RollbackTransactionAsync(cancellationToken);
                return new MarkTokenNonExecutableResult(request.TokenId, false, "Token not found");
            }

            token.MarkNonExecutable(request.Reason);

            await _uow.CommitTransactionAsync(cancellationToken);
            return new MarkTokenNonExecutableResult(request.TokenId, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[MARK-TOKEN-NONEXEC] Failed. TokenId={TokenId}", request.TokenId);
            await _uow.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}

