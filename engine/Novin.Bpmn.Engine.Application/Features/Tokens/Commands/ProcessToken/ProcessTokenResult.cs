namespace Novin.Bpmn.Engine.Application.Commands.ProcessToken;

public sealed class ProcessTokenResult
{
    public Guid TokenId { get; }
    public bool Success { get; }
    public string? Error { get; }

    public ProcessTokenResult(Guid tokenId, bool success, string? error = null)
    {
        TokenId = tokenId;
        Success = success;
        Error = error;
    }
}

