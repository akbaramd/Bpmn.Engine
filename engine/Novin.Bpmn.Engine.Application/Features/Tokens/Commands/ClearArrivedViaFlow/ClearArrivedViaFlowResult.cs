namespace Novin.Bpmn.Engine.Application.Commands.ClearArrivedViaFlow;

public sealed class ClearArrivedViaFlowResult
{
    public Guid TokenId { get; }
    public bool Success { get; }
    public string? Error { get; }

    public ClearArrivedViaFlowResult(Guid tokenId, bool success, string? error = null)
    {
        TokenId = tokenId;
        Success = success;
        Error = error;
    }
}

