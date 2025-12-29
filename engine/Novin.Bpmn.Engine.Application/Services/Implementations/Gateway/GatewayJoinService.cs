using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Models.Models;
using System;
using System.Collections.Generic;

namespace Novin.Bpmn.Engine.Application.Services;

public sealed class GatewayJoinService : IGatewayJoinService
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<GatewayJoinService> _logger;

    // marker to "close" merge/join for a scope
    private const string ClosedPrefix = "__novin.gw.closed:";

    public GatewayJoinService(IUnitOfWork uow, ILogger<GatewayJoinService> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> TryJoinAsync(
        Process process,
        Token arrivingToken,
        BpmnGateway gateway,
        BpmnRuntimeContext ctx,
        CancellationToken ct)
    {
        var incoming = ctx.Model.GetIncomingSequenceFlows(ctx.BpmnProcessId, arrivingToken.CurrentElementId);
        var outgoing = ctx.Model.GetOutgoingSequenceFlows(ctx.BpmnProcessId, arrivingToken.CurrentElementId);

        // we only handle merge/join gateways here (incoming>1) AND typical gateway (outgoing==1)
        if (incoming.Count <= 1 || outgoing.Count != 1)
            return false;

        // Rule 3: Join only makes sense with correlation (ScopeId)
        // If ScopeId is null => Fail (because split was not done correctly)
        if (arrivingToken.ScopeId == null)
        {
            _logger.LogError(
                "[JOIN] Token arrived at join gateway without ScopeId. This indicates split was not done correctly. TokenId={TokenId} GatewayId={GatewayId} IncomingCount={IncomingCount}",
                arrivingToken.Id, gateway.id, incoming.Count);
            arrivingToken.Fail("Join gateway requires ScopeId for correlation. Token arrived without ScopeId, indicating split was not performed correctly.");
            return true; // handled (failed)
        }

        var scopeId = arrivingToken.ScopeId.Value;
        var closedKey = ClosedKey(gateway.id, scopeId);

        // If gate is already closed => just consume this token and stop.
        if (process.HasVariable(closedKey))
        {
            Consume(arrivingToken, process, "Gateway already closed (late arrival).");
            return true;
        }

        // Read split metadata
        GatewaySplitService.TryReadExpectedTotal(process, scopeId, out var expectedTotal);
        GatewaySplitService.TryReadExpectedExec(process, scopeId, out var expectedExec);

        // If metadata missing, safe fallbacks:
        if (expectedTotal <= 0)
        {
            expectedTotal = DistinctIncomingCount(incoming);
        }
        if (expectedExec < 0)
        {
            expectedExec = 0;
        }

        var hasExecutableBranch = expectedExec > 0;

        // ------------------------
        // EXCLUSIVE MERGE (NOT join):
        // - executable token passes (closes gate)
        // - trace token:
        //    - if executable branch exists => trace must be consumed (never pass)
        //    - if NO executable branch exists => trace can pass (closes gate)
        // ------------------------
        if (gateway is BpmnExclusiveGateway)
        {
            if (!arrivingToken.IsExecutable && hasExecutableBranch)
            {
                Consume(arrivingToken, process, "Trace token consumed at XOR merge (executable path exists).");
                return true;
            }

            // winner (either executable, or trace-only group)
            Close(process, closedKey);

            arrivingToken.ClearArrivedVia();
            arrivingToken.ClearScope(); // scope ends here

            // return false => GatewayHandler will route normally (move to outgoing)
            return false;
        }

        // ------------------------
        // INCLUSIVE/PARALLEL JOIN:
        // - if executable branches exist: wait only for executable arrivals (trace tokens are consumed immediately)
        // - if no executable branches exist: treat trace tokens as join participants (wait for all trace arrivals)
        // ------------------------

        // If this token is trace but we have executable branches => consume immediately (do not wait)
        if (!arrivingToken.IsExecutable && hasExecutableBranch)
        {
            Consume(arrivingToken, process, "Trace token consumed at join (executable branches exist).");
            return true;
        }

        // Ensure token is waiting to avoid double-dispatch crashes
        if (arrivingToken.State == TokenState.Active)
        {
            arrivingToken.Wait("Join candidate - waiting for other branches");
        }
        else if (arrivingToken.State != TokenState.Waiting)
        {
            _logger.LogWarning("[JOIN] Token state={State} (expected Active/Waiting). Consuming defensively. TokenId={TokenId}",
                arrivingToken.State, arrivingToken.Id);
            Consume(arrivingToken, process, "Invalid state at join.");
            return true;
        }

        var requiredArrivals = hasExecutableBranch ? expectedExec : expectedTotal;

        // Build incoming key set for validation
        var incomingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < incoming.Count; i++)
            incomingKeys.Add(FlowKey(incoming[i]));

        // Load tokens in process (better: add a repository method to query waiting by element+scope)
        var allTokens = await _uow.Tokens.GetByProcessIdAsync(process.Id, ct);

        // Count arrivals (distinct ArrivedViaFlowId) among relevant waiting tokens
        // relevant = executable-only if hasExecutableBranch, else trace-only group => allow all waiting (all are trace here)
        var arrived = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var waitingTokens = new List<Token>(capacity: 8);

        foreach (var t in allTokens)
        {
            if (t.CurrentElementId != arrivingToken.CurrentElementId) continue;
            if (t.ScopeId != scopeId) continue;
            if (t.State != TokenState.Waiting) continue;

            if (hasExecutableBranch)
            {
                if (!t.IsExecutable) continue; // trace doesn't participate
            }
            else
            {
                // trace-only group => allow all (all should be trace; if not, still ok)
            }

            waitingTokens.Add(t);

            var via = t.ArrivedViaFlowId;
            if (!string.IsNullOrWhiteSpace(via) && incomingKeys.Contains(via!))
                arrived.Add(via!);
        }

        _logger.LogInformation(
            "[JOIN] Gw={Gw} Type={Type} ScopeId={ScopeId} HasExec={HasExec} Required={Req} Arrived={Arrived} Waiting={Waiting}",
            arrivingToken.CurrentElementId,
            gateway.GetType().Name,
            scopeId,
            hasExecutableBranch,
            requiredArrivals,
            arrived.Count,
            waitingTokens.Count);

        if (arrived.Count < requiredArrivals)
            return true; // still waiting

        // Ready to release join
        // Output executability: OR of inputs => equals "hasExecutableBranch"
        var outputExecutable = hasExecutableBranch;

        // Choose survivor deterministically:
        // - if outputExecutable => prefer executable (should all be executable here)
        // - else => trace-only group, just pick oldest
        Token survivor = PickSurvivor(waitingTokens, preferExecutable: outputExecutable);

        // Consume non-survivors
        for (var i = 0; i < waitingTokens.Count; i++)
        {
            var t = waitingTokens[i];
            if (t.Id == survivor.Id) continue;

            t.Terminate("Merged into survivor at join gateway.");
            process.RemoveToken(t.Id);
        }

        // Adjust survivor executability if needed
        if (!outputExecutable && survivor.IsExecutable)
        {
            survivor.MarkNonExecutable("Join output: all inputs were trace tokens");
        }

        // Close gate (important for inclusive: late trace arrivals must be consumed)
        Close(process, closedKey);

        // Continue with survivor
        survivor.ResumeWithoutProcessing();
        survivor.ClearArrivedVia();
        survivor.ClearScope();

        var outFlow = outgoing[0];
        if (string.IsNullOrWhiteSpace(outFlow.targetRef))
            throw new InvalidOperationException("Join gateway must have exactly one outgoing with targetRef.");

        survivor.MoveTo(outFlow.targetRef!, FlowKey(outFlow));
        return true;
    }

    // ---------------- helpers ----------------

    private static string ClosedKey(string gatewayId, Guid scopeId)
        => $"{ClosedPrefix}{gatewayId}:{scopeId:N}";

    private static void Close(Process p, string closedKey)
        => p.SetVariable(closedKey, "1");

    private static void Consume(Token t, Process p, string reason)
    {
        // Important: do NOT crash if already terminal
        if (t.State != TokenState.Completed && t.State != TokenState.Terminated)
        {
            t.Terminate(reason);
        }
        p.RemoveToken(t.Id);
    }

    private static int DistinctIncomingCount(List<BpmnSequenceFlow> incoming)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < incoming.Count; i++)
            set.Add(FlowKey(incoming[i]));
        return set.Count;
    }

    private static Token PickSurvivor(List<Token> waiting, bool preferExecutable)
    {
        Token? best = null;

        for (var i = 0; i < waiting.Count; i++)
        {
            var t = waiting[i];

            if (best == null)
            {
                best = t;
                continue;
            }

            if (preferExecutable)
            {
                if (t.IsExecutable && !best.IsExecutable)
                {
                    best = t;
                    continue;
                }
                if (t.IsExecutable == best.IsExecutable && t.CreatedAt < best.CreatedAt)
                {
                    best = t;
                    continue;
                }
            }
            else
            {
                if (t.CreatedAt < best.CreatedAt)
                {
                    best = t;
                    continue;
                }
            }
        }

        return best ?? waiting[0];
    }

    private static string FlowKey(BpmnSequenceFlow f)
        => !string.IsNullOrWhiteSpace(f.id) ? f.id! : $"{f.sourceRef}->{f.targetRef}";
}
