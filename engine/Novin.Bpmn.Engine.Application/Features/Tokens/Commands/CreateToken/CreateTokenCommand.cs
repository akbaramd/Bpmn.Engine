using MediatR;

namespace Novin.Bpmn.Engine.Application.Commands.CreateToken;

public sealed record CreateTokenCommand(
    Guid ProcessId,
    string StartElementId,
    Guid? ParentTokenId = null,
    string? ArrivedViaFlowId = null,
    bool IsExecutable = true,
    Guid? ScopeId = null,
    IReadOnlyDictionary<string, string>? Variables = null) : IRequest<CreateTokenResult>;

