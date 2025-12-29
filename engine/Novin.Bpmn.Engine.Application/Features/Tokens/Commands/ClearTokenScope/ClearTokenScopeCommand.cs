using MediatR;

namespace Novin.Bpmn.Engine.Application.Commands.ClearTokenScope;

public sealed record ClearTokenScopeCommand(Guid ProcessId, Guid TokenId) : IRequest<ClearTokenScopeResult>;

