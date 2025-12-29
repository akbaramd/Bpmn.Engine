namespace Novin.Bpmn.Engine.Application.Commands.WaitToken;

public sealed class WaitTokenResult
{
    public Guid TokenId { get; }
    public bool Success { get; }
    public string? Error { get; }

    public WaitTokenResult(Guid tokenId, bool success, string? error = null)
    {
        TokenId = tokenId;
        Success = success;
        Error = error;
    }
}

