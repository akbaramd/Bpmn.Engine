using MediatR;

namespace Novin.Bpmn.Engine.Application.Commands.CreateMergedToken;

/// <summary>
/// Command to create a merged token after Join/Merge gateway satisfaction.
/// This token represents the result of merging multiple input tokens.
/// 
/// Policy:
/// - All input tokens are terminated (no winner continues)
/// - A new merged token is created and activated
/// - Idempotent: if merged token already exists for this join, returns existing token ID
/// </summary>
public sealed record CreateMergedTokenCommand(
    Guid ProcessId,
    string GatewayId,
    Guid ScopeId,
    IReadOnlyList<Guid>? ParentTokenIds = null,
    IEnumerable<string>? ArrivedViaFlowIds = null
) : IRequest<Guid>;
