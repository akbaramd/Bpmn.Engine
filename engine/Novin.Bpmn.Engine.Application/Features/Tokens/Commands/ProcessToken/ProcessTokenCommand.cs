using MediatR;

namespace Novin.Bpmn.Engine.Application.Commands.ProcessToken;

/// <summary>
/// Command to process a token at its current element.
/// </summary>
/// <param name="ProcessId">Process ID</param>
/// <param name="TokenId">Token ID</param>
/// <param name="IsResume">True if this is a resume operation (token was waiting and is now resuming), false for normal processing</param>
public sealed record ProcessTokenCommand(
    Guid ProcessId, 
    Guid TokenId,
    bool IsResume = false) : IRequest<ProcessTokenResult>;

