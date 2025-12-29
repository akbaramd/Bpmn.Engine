using MediatR;

namespace Novin.Bpmn.Engine.Application.Commands.WaitToken;

public sealed record WaitTokenCommand(
    Guid ProcessId,
    Guid TokenId,
    string? Reason = null,
    Guid? WorkerId = null) : IRequest<WaitTokenResult>;

