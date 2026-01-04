using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

    public async Task<CreateTokenResult> Handle(CreateTokenCommand request, CancellationToken ct)
    {
        // ---- Load process (caller manages transaction) ----
        var process = await _uow.Processes.GetByIdAsync(request.ProcessId, ct);
        if (process is null)
            return new CreateTokenResult(Guid.Empty, request.ProcessId, false, "Process not found");

        if (string.IsNullOrWhiteSpace(request.StartElementId))
            return new CreateTokenResult(Guid.Empty, request.ProcessId, false, "StartElementId is empty");

        // ---- Create token aggregate ----
        var token = new Token(request.ProcessId, request.StartElementId, request.ParentTokenId);

        // ---- Arrived-via ----
        if (!string.IsNullOrWhiteSpace(request.ArrivedViaFlowId))
            token.SetArrivedVia(request.ArrivedViaFlowId);

        // ---- Scope / ScopeStack (Zeebe-like correlation rules) ----
        ApplyScopesWithGuards(token, request);

        // ---- Variables (defensive snapshot) ----
        if (request.Variables is { Count: > 0 })
        {
            // even if caller already cloned, we keep deterministic behavior
            foreach (var kv in request.Variables)
            {
                // if your dictionary is <string,string?> adjust Token.SetVariable signature
                token.SetVariable(kv.Key, kv.Value);
            }
        }

        // ---- Activate + persist ----
        token.Activate();
        token.MoveTo(request.StartElementId,false,[request.ArrivedViaFlowId]);
        await _uow.Tokens.AddAsync(token, ct);
        process.AddToken(token.Id);
        await _uow.Processes.UpdateAsync(process, ct);

        _logger.LogInformation(
            "[CREATE-TOKEN] Created. Proc={Proc} Token={Token} StartEl={El} Parent={Parent} Scope={Scope} Depth={Depth}",
            request.ProcessId,
            token.Id,
            request.StartElementId,
            token.ParentTokenId,
            token.ScopeId,
            token.ScopeStack?.Count ?? 0);

        return new CreateTokenResult(token.Id, request.ProcessId, true);
    }

    private void ApplyScopesWithGuards(Token token, CreateTokenCommand request)
    {
        var parentId = request.ParentTokenId;
        var hasParent = parentId.HasValue && parentId.Value != Guid.Empty;

        var stack = request.ScopeStackSnapshot;
        var hasStack = stack is { Count: > 0 } && stack.Any(x => x != Guid.Empty);

        var scopeId = request.ScopeId;
        var hasSingleScope = scopeId.HasValue && scopeId.Value != Guid.Empty;

        // Invariant: correlation scopes only make sense with ParentTokenId
        if (!hasParent)
        {
            if (hasStack || hasSingleScope)
            {
                _logger.LogWarning(
                    "[CREATE-TOKEN:SCOPE] Scope provided WITHOUT ParentTokenId => DETACH. " +
                    "Proc={Proc} Token={Token} StartEl={El} Parent={Parent} ScopeId={ScopeId} StackCount={StackCount}",
                    request.ProcessId,
                    token.Id,
                    request.StartElementId,
                    request.ParentTokenId,
                    request.ScopeId,
                    request.ScopeStackSnapshot?.Count ?? 0);
            }

            // ✅ Detach: do not apply any scope
            token.ClearAllScopes(); // safe no-op if empty
            return;
        }

        // Prefer stack snapshot (nested correlation)
        if (hasStack)
        {
            token.ReplaceScopeStack(stack!);
            return;
        }

        // Backward compatibility: single scope
        if (hasSingleScope)
        {
            token.PushScope(scopeId!.Value); // or token.SetScope if alias
            return;
        }

        // Parent exists but no scope info was provided.
        // This is allowed in "detached" or "non-join-participating" branches.
        // But it is suspicious if you expected join correlation. Log at Debug.
        _logger.LogDebug(
            "[CREATE-TOKEN:SCOPE] Parent provided but no scope provided (detached branch). " +
            "Proc={Proc} StartEl={El} Parent={Parent}",
            request.ProcessId,
            request.StartElementId,
            request.ParentTokenId);

        token.ClearAllScopes();
    }
}
