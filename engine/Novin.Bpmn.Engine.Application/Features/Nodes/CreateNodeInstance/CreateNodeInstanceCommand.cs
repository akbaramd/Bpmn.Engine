using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Application.Commands.NodeInstances;

// ======================================================
// Command
// ======================================================
public sealed record CreateNodeInstanceCommand(
    Guid ProcessId,
    Guid TokenId,
    string ElementId,
    bool IsExecutable,
    Guid? ScopeId,
    Guid? ActivityInstanceId,
    IEnumerable<string>? ArrivedViaFlowIds = null
) : IRequest<Guid>;

// ======================================================
// Handler
// ======================================================
public sealed class CreateNodeInstanceCommandHandler : IRequestHandler<CreateNodeInstanceCommand, Guid>
{
    private readonly IUnitOfWork _uow;
    private readonly INodeInstanceRepository _nodes;
    private readonly ITokenRepository _tokens;
    private readonly IProcessRepository _processes;
    private readonly ILogger<CreateNodeInstanceCommandHandler> _logger;

    public CreateNodeInstanceCommandHandler(
        IUnitOfWork uow,
        INodeInstanceRepository nodes,
        ITokenRepository tokens,
        IProcessRepository processes,
        ILogger<CreateNodeInstanceCommandHandler> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _processes = processes ?? throw new ArgumentNullException(nameof(processes));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Guid> Handle(CreateNodeInstanceCommand request, CancellationToken cancellationToken)
    {
        if (request.ProcessId == Guid.Empty) throw new ArgumentException("ProcessId cannot be empty.", nameof(request));
        if (request.TokenId == Guid.Empty) throw new ArgumentException("TokenId cannot be empty.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.ElementId)) throw new ArgumentException("ElementId is required.", nameof(request));
        await _uow.BeginTransactionAsync();
        // ------------------------------------------------------------
        // 1) Load Token (authoritative source for current element/state)
        // ------------------------------------------------------------
        var token = await _tokens.GetByIdAsync(request.TokenId, cancellationToken);
        if (token is null)
            throw new InvalidOperationException($"Token '{request.TokenId}' not found.");

        if (token.ProcessId != request.ProcessId)
            throw new InvalidOperationException("Token does not belong to the given ProcessId.");

        // Guard: token must be executable to create a NodeInstance
        if (!token.IsExecutable)
        {
            _logger.LogDebug("Skip NodeInstance creation. Token {TokenId} is non-executable (trace token).", token.Id);
            return Guid.Empty; // explicit "no node created"
        }
 
        // Guard: element id must match current token position (prevents stale commands)
        var elementId = request.ElementId.Trim();
        if (!string.Equals(token.CurrentElementId, elementId, StringComparison.Ordinal))
            throw new InvalidOperationException("Stale CreateNodeInstanceCommand: token.CurrentElementId mismatch.");

        // ------------------------------------------------------------
        // 2) Idempotency: if an open NodeInstance already exists for this correlation, reuse it
        // ------------------------------------------------------------
        // Convert Token's single ArrivedViaFlowId to array for comparison
        var arrivedViaFlowIds = request.ArrivedViaFlowIds ?? 
            (string.IsNullOrWhiteSpace(token.ArrivedViaFlowId) 
                ? null 
                : new[] { token.ArrivedViaFlowId });
        
        var existing = await _nodes.TryFindOpenAsync(
            processId: token.ProcessId,
            tokenId: token.Id,
            elementId: elementId,
            scopeId: token.ScopeId,
            activityInstanceId: token.ActivityInstanceId,
            arrivedViaFlowIds: arrivedViaFlowIds,
            cancellationToken: cancellationToken);

        if (existing is not null)
        {
            return existing.Id;
        }

        
        token.SetActivityInstance(Guid.NewGuid());
        // ------------------------------------------------------------
        // 3) Create NodeInstance aggregate (raises NodeCreatedDomainEvent)
        // ------------------------------------------------------------
        var node = new NodeInstance(
            processId: token.ProcessId,
            tokenId: token.Id,
            elementId: elementId,
            isExecutable:request.IsExecutable,
            scopeId: token.ScopeId,
            activityInstanceId: token.ActivityInstanceId,
            arrivedViaFlowIds: arrivedViaFlowIds);

        await _nodes.AddAsync(node, cancellationToken);

        // ------------------------------------------------------------
        // 4) Register on Process (IDs only)
        // ------------------------------------------------------------
        var process = await _processes.GetByIdAsync(token.ProcessId, cancellationToken);
        if (process is null)
            throw new InvalidOperationException($"Process '{token.ProcessId}' not found.");

        process.RegisterNodeInstance(node.Id);
        await _processes.UpdateAsync(process, cancellationToken);

        await _uow.CommitTransactionAsync();
        // NOTE: Do NOT commit here. Commit happens at outer transaction boundary (UnitOfWork)
        return node.Id;
    }
}
