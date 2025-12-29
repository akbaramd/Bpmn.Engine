namespace Novin.Bpmn.Engine.Application.Commands.ClearTokenLocalVariables;

public sealed class ClearTokenLocalVariablesResult
{
    public Guid TokenId { get; }
    public bool Success { get; }
    public string? Error { get; }

    public ClearTokenLocalVariablesResult(Guid tokenId, bool success, string? error = null)
    {
        TokenId = tokenId;
        Success = success;
        Error = error;
    }
}

