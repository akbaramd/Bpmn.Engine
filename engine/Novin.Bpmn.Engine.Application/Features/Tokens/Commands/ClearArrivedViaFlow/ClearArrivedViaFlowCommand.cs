using MediatR;

namespace Novin.Bpmn.Engine.Application.Commands.ClearArrivedViaFlow;

public sealed record ClearArrivedViaFlowCommand(Guid ProcessId, Guid TokenId) : IRequest<ClearArrivedViaFlowResult>;

