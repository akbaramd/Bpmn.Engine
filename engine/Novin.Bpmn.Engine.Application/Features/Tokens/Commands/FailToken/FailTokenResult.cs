namespace Novin.Bpmn.Engine.Application.Commands.FailToken;

/// <summary>
/// Result of failing a token
/// </summary>
public record FailTokenResult(
    Guid TokenId,
    bool WasFailed,
    Guid? IncidentId = null,
    string? Reason = null
);