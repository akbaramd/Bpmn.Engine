using System.Text.Json.Nodes;
using MediatR;

namespace Novin.Bpmn.Engine.Application.Commands.CreateToken;


public sealed record CreateTokenCommand(
    Guid ProcessId,
    string StartElementId,
    Guid? ParentTokenId = null,
    string? ArrivedViaFlowId = null,
    Guid? ScopeId = null, // backward compatibility
    IReadOnlyDictionary<string, JsonNode?>? Variables = null,
    IReadOnlyList<Guid>? ScopeStackSnapshot = null
) : IRequest<CreateTokenResult>;

