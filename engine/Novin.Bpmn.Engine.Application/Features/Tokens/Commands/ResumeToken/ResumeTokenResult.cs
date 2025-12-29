namespace Novin.Bpmn.Engine.Application.Commands.ResumeToken;

public sealed class ResumeTokenResult
{
    public Guid TokenId { get; }
    public bool Success { get; }
    public string? Error { get; }

    public ResumeTokenResult(Guid tokenId, bool success, string? error = null)
    {
        TokenId = tokenId;
        Success = success;
        Error = error;
    }
}

