using MediatR;

namespace Novin.Bpmn.Engine.Application.Commands.RetryToken;

public sealed record RetryTokenCommand(Guid ProcessId, Guid TokenId) : IRequest<RetryTokenResult>;

