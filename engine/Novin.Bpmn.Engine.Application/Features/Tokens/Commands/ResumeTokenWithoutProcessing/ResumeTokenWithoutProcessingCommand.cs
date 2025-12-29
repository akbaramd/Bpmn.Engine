using MediatR;

namespace Novin.Bpmn.Engine.Application.Commands.ResumeTokenWithoutProcessing;

public sealed record ResumeTokenWithoutProcessingCommand(Guid ProcessId, Guid TokenId) : IRequest<ResumeTokenWithoutProcessingResult>;

