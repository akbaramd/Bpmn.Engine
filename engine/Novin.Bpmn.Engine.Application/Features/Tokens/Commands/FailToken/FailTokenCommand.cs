using MediatR;

namespace Novin.Bpmn.Engine.Application.Commands.FailToken;

/// <summary>
/// Command to fail a token
/// </summary>
public record FailTokenCommand(
    Guid ProcessId,
    Guid TokenId,
    string ErrorMessage,
    string ErrorType = "TechnicalFailure",
    string? ErrorCode = null
) : IRequest<FailTokenResult>;