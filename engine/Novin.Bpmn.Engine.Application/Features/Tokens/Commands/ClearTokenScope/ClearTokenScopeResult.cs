namespace Novin.Bpmn.Engine.Application.Commands.ClearTokenScope;

public sealed class ClearTokenScopeResult
{
    public Guid TokenId { get; }
    public bool Success { get; }
    public string? Error { get; }

    public ClearTokenScopeResult(Guid tokenId, bool success, string? error = null)
    {
        TokenId = tokenId;
        Success = success;
        Error = error;
    }
}

