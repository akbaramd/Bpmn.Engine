using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Application.Commands.CreateToken;

public sealed class CreateTokenCommandHandler : IRequestHandler<CreateTokenCommand, CreateTokenResult>
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<CreateTokenCommandHandler> _logger;

    public CreateTokenCommandHandler(IUnitOfWork uow, ILogger<CreateTokenCommandHandler> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CreateTokenResult> Handle(CreateTokenCommand request, CancellationToken cancellationToken)
    {
        await _uow.BeginTransactionAsync(cancellationToken);
        try
        {
            var process = await _uow.Processes.GetByIdAsync(request.ProcessId, cancellationToken);
            if (process is null)
            {
                await _uow.RollbackTransactionAsync(cancellationToken);
                return new CreateTokenResult(Guid.Empty, request.ProcessId, false, "Process not found");
            }

            var parentIds = request.ParentTokenIds?.Where(id => id != Guid.Empty).Distinct() ?? Enumerable.Empty<Guid>();
            var token = new Token(request.ProcessId, request.StartElementId, parentIds);

            if (!string.IsNullOrWhiteSpace(request.ArrivedViaFlowId))
            {
                token.SetArrivedVia(request.ArrivedViaFlowId);
            }

            // Set executable flag if provided
            if (!request.IsExecutable)
            {
                token.MarkNonExecutable();
            }

            // Set scope if provided
            if (request.ScopeId.HasValue && request.ScopeId.Value != Guid.Empty)
            {
                token.SetScope(request.ScopeId.Value);
            }

            // Set variables if provided
            if (request.Variables != null)
            {
                foreach (var kv in request.Variables)
                {
                    token.SetVariable(kv.Key, kv.Value);
                }
            }

            token.Activate();
            await _uow.Tokens.AddAsync(token, cancellationToken);
            process.AddToken(token.Id);

            await _uow.CommitTransactionAsync(cancellationToken);

            return new CreateTokenResult(token.Id, request.ProcessId, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CREATE-TOKEN] Failed. ProcessId={ProcessId} StartElementId={StartElementId}", request.ProcessId, request.StartElementId);
            await _uow.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}

