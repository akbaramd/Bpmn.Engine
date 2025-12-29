using MediatR;

namespace Novin.Bpmn.Engine.Application.Commands.TerminateToken;

/// <summary>
/// Command to terminate a token
/// </summary>
public record TerminateTokenCommand(
    Guid ProcessId,
    Guid TokenId,
    string? Reason = null
) : IRequest<TerminateTokenResult>;