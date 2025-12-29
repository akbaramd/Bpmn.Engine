using MediatR;

namespace Novin.Bpmn.Engine.Application.Commands.ResumeToken;

public sealed record ResumeTokenCommand(Guid ProcessId, Guid TokenId) : IRequest<ResumeTokenResult>;

