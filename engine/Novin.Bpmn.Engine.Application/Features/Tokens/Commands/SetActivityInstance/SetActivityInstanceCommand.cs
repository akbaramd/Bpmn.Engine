using MediatR;

namespace Novin.Bpmn.Engine.Application.Commands.SetActivityInstance;

public sealed record SetActivityInstanceCommand(Guid ProcessId, Guid TokenId, Guid ActivityInstanceId) : IRequest<SetActivityInstanceResult>;

