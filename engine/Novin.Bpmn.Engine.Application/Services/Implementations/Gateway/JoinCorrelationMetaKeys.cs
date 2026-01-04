namespace Novin.Bpmn.Engine.Application.Services;

/// <summary>
/// ✅ Single source of truth for ALL join-correlation metadata keys.
/// Split + Join MUST use these exact keys.
/// </summary>


public static class JoinCorrelationMetaKeys
{
    // Scope keys (scopeId = fork instance)
    public static string ExpectedCount(Guid scopeId) => $"join:{scopeId:N}:expectedCount";
    public static string SplitGatewayId(Guid scopeId) => $"join:{scopeId:N}:splitGwId";     // debug only
    public static string SplitGatewayType(Guid scopeId) => $"join:{scopeId:N}:splitGwType"; // debug only
    public static string Branches(Guid scopeId) => $"join:{scopeId:N}:branches";            // debug only
}