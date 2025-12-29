using MediatR;

namespace Novin.Bpmn.Engine.Application.Commands.MarkTokenNonExecutable;

public sealed record MarkTokenNonExecutableCommand(Guid ProcessId, Guid TokenId, string? Reason = null) : IRequest<MarkTokenNonExecutableResult>;

