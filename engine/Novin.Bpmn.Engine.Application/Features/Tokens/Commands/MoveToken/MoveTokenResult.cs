namespace Novin.Bpmn.Engine.Application.Commands.MoveToken;

public sealed class MoveTokenResult
{
    public Guid TokenId { get; }
    public bool Success { get; }
    public string? Error { get; }

    public MoveTokenResult(Guid tokenId, bool success, string? error = null)
    {
        TokenId = tokenId;
        Success = success;
        Error = error;
    }
}

