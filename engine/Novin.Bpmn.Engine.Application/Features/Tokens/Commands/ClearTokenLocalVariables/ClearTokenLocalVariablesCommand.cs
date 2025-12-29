using MediatR;

namespace Novin.Bpmn.Engine.Application.Commands.ClearTokenLocalVariables;

public sealed record ClearTokenLocalVariablesCommand(Guid ProcessId, Guid TokenId) : IRequest<ClearTokenLocalVariablesResult>;

