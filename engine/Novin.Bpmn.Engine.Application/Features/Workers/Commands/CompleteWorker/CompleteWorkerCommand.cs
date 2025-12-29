using MediatR;

namespace Novin.Bpmn.Engine.Application.Features.Workers.Commands;

public record CompleteWorkerCommand(
    Guid WorkerId,
    Dictionary<string, string>? Result,
    string? CompletedBy
) : IRequest;