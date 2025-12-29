namespace Novin.Bpmn.Engine.Application.Commands.MarkTokenNonExecutable;

public sealed class MarkTokenNonExecutableResult
{
    public Guid TokenId { get; }
    public bool Success { get; }
    public string? Error { get; }

    public MarkTokenNonExecutableResult(Guid tokenId, bool success, string? error = null)
    {
        TokenId = tokenId;
        Success = success;
        Error = error;
    }
}

