namespace Novin.Bpmn.Engine.Application.Commands.RetryToken;

public sealed class RetryTokenResult
{
    public Guid TokenId { get; }
    public bool Success { get; }
    public string? Error { get; }

    public RetryTokenResult(Guid tokenId, bool success, string? error = null)
    {
        TokenId = tokenId;
        Success = success;
        Error = error;
    }
}

