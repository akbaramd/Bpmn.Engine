using MediatR;

namespace Novin.Bpmn.Engine.Application.Commands.ClearActivityInstance;

public sealed record ClearActivityInstanceCommand(Guid ProcessId, Guid TokenId) : IRequest<ClearActivityInstanceResult>;

