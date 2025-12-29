using MediatR;

namespace Novin.Bpmn.Engine.Application.Commands.AssignScopeToToken;

public sealed record AssignScopeToTokenCommand(Guid ProcessId, Guid TokenId, Guid ScopeId) : IRequest<AssignScopeToTokenResult>;

