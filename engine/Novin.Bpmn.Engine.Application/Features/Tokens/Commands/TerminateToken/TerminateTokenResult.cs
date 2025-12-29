namespace Novin.Bpmn.Engine.Application.Commands.TerminateToken;

/// <summary>
/// Result of terminating a token
/// </summary>
public record TerminateTokenResult(
    Guid TokenId,
    bool WasTerminated,
    string? Reason = null
);