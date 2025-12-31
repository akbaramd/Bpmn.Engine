// Domain/ValueObjects/GatewayScopeKeys.cs
using System;

namespace Novin.Bpmn.Engine.Domain.ValueObjects;

/// <summary>
/// Centralized key factory for gateway-scope variables (fork/join correlation).
/// Keep ALL prefixes/format rules here to prevent split/join key mismatch bugs.
/// </summary>
public static class GatewayScopeKeys
{
    // ---------------------------------------------------------------------
    // Scope-based (written by GatewaySplitService)  => keyed ONLY by ScopeId
    // ---------------------------------------------------------------------
    public const string ScopeExpectedTotalPrefix = "__novin.scope.expectedTotal:";
    public const string ScopeExpectedExecPrefix  = "__novin.scope.expectedExec:";

    public static string ScopeExpectedTotal(Guid scopeId) => $"{ScopeExpectedTotalPrefix}{scopeId:N}";
    public static string ScopeExpectedExec(Guid scopeId)  => $"{ScopeExpectedExecPrefix}{scopeId:N}";

    // ---------------------------------------------------------------------
    // Gateway+Scope-based (written by GatewayJoin logic) => keyed by GwId + ScopeId
    // ---------------------------------------------------------------------
    public const string GwClosedPrefix   = "gw:closed:";
    public const string GwWinnerPrefix   = "gw:winner:"; // ⚠️ DEPRECATED: No winner concept, use GwMergedToken instead
    public const string GwConsumedPrefix = "gw:consumed:";
    public const string GwMergedTokenPrefix = "gw:mergedToken:";

    public static string GwClosed(string gatewayId, Guid scopeId)   => $"{GwClosedPrefix}{gatewayId}:{scopeId:N}";
    public static string GwWinner(string gatewayId, Guid scopeId)   => $"{GwWinnerPrefix}{gatewayId}:{scopeId:N}"; // ⚠️ DEPRECATED
    public static string GwConsumed(string gatewayId, Guid scopeId) => $"{GwConsumedPrefix}{gatewayId}:{scopeId:N}";
    public static string GwMergedToken(string gatewayId, Guid scopeId) => $"{GwMergedTokenPrefix}{gatewayId}:{scopeId:N}";
}