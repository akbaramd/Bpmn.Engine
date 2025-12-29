using MediatR;

namespace Novin.Bpmn.Engine.Application.Features.Workers.Commands;

public record FailWorkerCommand(
    Guid WorkerId,
    string Error,
    string? CompletedBy
) : IRequest;