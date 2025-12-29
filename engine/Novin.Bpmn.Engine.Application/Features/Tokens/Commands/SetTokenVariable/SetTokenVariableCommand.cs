using MediatR;

namespace Novin.Bpmn.Engine.Application.Commands.SetTokenVariable;

public sealed record SetTokenVariableCommand(Guid ProcessId, Guid TokenId, string Name, object? Value) : IRequest<SetTokenVariableResult>;

