namespace Novin.Bpmn.Engine.Application.Commands.AssignScopeToToken;

public sealed class AssignScopeToTokenResult
{
    public Guid TokenId { get; }
    public bool Success { get; }
    public string? Error { get; }

    public AssignScopeToTokenResult(Guid tokenId, bool success, string? error = null)
    {
        TokenId = tokenId;
        Success = success;
        Error = error;
    }
}

