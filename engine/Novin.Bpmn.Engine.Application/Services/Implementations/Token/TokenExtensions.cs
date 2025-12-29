using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.Services;

/// <summary>
/// Determines if a token is "active" (should be converted to trace token).
/// Active tokens are in Created/Active/Waiting states.
/// </summary>
internal static class TokenExtensions
{
    internal static bool IsActiveToken(Token token)
    {
        return token.State is TokenState.Created
            or TokenState.Active
            or TokenState.Waiting;
    }
}