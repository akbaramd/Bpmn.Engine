using MediatR;

namespace Novin.Bpmn.Engine.Application.Commands.SetArrivedViaFlow;

public sealed record SetArrivedViaFlowCommand(Guid ProcessId, Guid TokenId, string FlowId) : IRequest<SetArrivedViaFlowResult>;

