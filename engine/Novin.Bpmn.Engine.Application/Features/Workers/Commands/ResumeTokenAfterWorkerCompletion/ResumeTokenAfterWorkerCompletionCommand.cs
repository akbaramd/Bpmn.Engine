using MediatR;

namespace Novin.Bpmn.Engine.Application.Commands;

public record ResumeTokenAfterWorkerCompletionCommand(
    Guid WorkerId,
    IReadOnlyDictionary<string, string>? Result,
    string? CompletedBy
) : IRequest;