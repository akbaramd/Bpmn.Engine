namespace Novin.Bpmn.Engine.Application.Commands.SetTokenVariable;

public sealed class SetTokenVariableResult
{
    public Guid TokenId { get; }
    public bool Success { get; }
    public string? Error { get; }

    public SetTokenVariableResult(Guid tokenId, bool success, string? error = null)
    {
        TokenId = tokenId;
        Success = success;
        Error = error;
    }
}

