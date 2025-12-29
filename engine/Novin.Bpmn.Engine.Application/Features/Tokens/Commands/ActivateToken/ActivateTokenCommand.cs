using MediatR;

namespace Novin.Bpmn.Engine.Application.Commands.ActivateToken;

/// <summary>
/// Command to activate a token for processing
/// </summary>
public record ActivateTokenCommand(
    Guid ProcessId,
    Guid TokenId,
    string? ArrivedViaFlowId = null
) : IRequest<ActivateTokenResult>;