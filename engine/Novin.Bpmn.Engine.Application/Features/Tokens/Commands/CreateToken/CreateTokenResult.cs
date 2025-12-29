namespace Novin.Bpmn.Engine.Application.Commands.CreateToken;

public sealed class CreateTokenResult
{
    public Guid TokenId { get; }
    public Guid ProcessId { get; }
    public bool Success { get; }
    public string? Error { get; }

    public CreateTokenResult(Guid tokenId, Guid processId, bool success, string? error = null)
    {
        TokenId = tokenId;
        ProcessId = processId;
        Success = success;
        Error = error;
    }
}

