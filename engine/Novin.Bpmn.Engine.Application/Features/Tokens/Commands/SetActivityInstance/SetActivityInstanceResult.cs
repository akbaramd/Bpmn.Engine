namespace Novin.Bpmn.Engine.Application.Commands.SetActivityInstance;

public sealed class SetActivityInstanceResult
{
    public Guid TokenId { get; }
    public bool Success { get; }
    public string? Error { get; }

    public SetActivityInstanceResult(Guid tokenId, bool success, string? error = null)
    {
        TokenId = tokenId;
        Success = success;
        Error = error;
    }
}

