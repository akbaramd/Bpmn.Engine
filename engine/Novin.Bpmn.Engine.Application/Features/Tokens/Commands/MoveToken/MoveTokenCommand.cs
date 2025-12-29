using MediatR;

namespace Novin.Bpmn.Engine.Application.Commands.MoveToken;

/// <summary>
/// Command to move a token from its current element to the next element.
/// This command ONLY performs movement - routing/forking/completing is done in element handlers.
/// </summary>
public sealed record MoveTokenCommand(
    Guid ProcessId,
    Guid TokenId,
    string NextElementId,
    string? ViaFlowId = null) : IRequest<MoveTokenResult>;

