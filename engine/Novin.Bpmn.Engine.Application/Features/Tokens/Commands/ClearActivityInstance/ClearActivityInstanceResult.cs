namespace Novin.Bpmn.Engine.Application.Commands.ClearActivityInstance;

public sealed class ClearActivityInstanceResult
{
    public Guid TokenId { get; }
    public bool Success { get; }
    public string? Error { get; }

    public ClearActivityInstanceResult(Guid tokenId, bool success, string? error = null)
    {
        TokenId = tokenId;
        Success = success;
        Error = error;
    }
}

