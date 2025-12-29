namespace Novin.Bpmn.Engine.Application.Commands.ActivateToken;

/// <summary>
/// Result of activating a token
/// </summary>
public record ActivateTokenResult(
    Guid TokenId,
    bool WasActivated,
    string? Reason = null
);