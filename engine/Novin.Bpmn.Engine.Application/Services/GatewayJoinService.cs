using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Models.Models;

public sealed class GatewayJoinService : IGatewayJoinService
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<GatewayJoinService> _logger;

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

        var isJoinCandidate = incoming.Count > 1 && outgoing.Count == 1;
        if (!isJoinCandidate)
            return false;

        // arriving باید Active باشد تا بتواند Wait کند (برای جلوگیری از crash در duplicate dispatch)
        if (arrivingToken.State == TokenState.Active)
        {
            arrivingToken.Wait("Join candidate - waiting for other branches");
        }
        else if (arrivingToken.State != TokenState.Waiting)
        {
            _logger.LogWarning("[JOIN] Token state is {State} (expected Active/Waiting). Ignoring join.", arrivingToken.State);
            return true; // handled defensively
        }

        // Ensure scope exists
        if (arrivingToken.ScopeId == null)
        {
            var fallback = arrivingToken.ParentTokenIds.FirstOrDefault();
            arrivingToken.SetScope(fallback != Guid.Empty ? fallback : arrivingToken.Id);
        }

        var scopeId = arrivingToken.ScopeId!.Value;

        // ✅ ExpectedCount from Split (scope metadata)
        if (!GatewaySplitService.TryReadExpectedCount(process, scopeId, out var expectedCount))
        {
            expectedCount = incoming.Count; // fallback (parallel-like)
            _logger.LogWarning("[JOIN] ExpectedCount not found for ScopeId={ScopeId}. Fallback ExpectedCount=IncomingCount={Cnt}.",
                scopeId, expectedCount);
        }

        var allTokens = await _uow.Tokens.GetByProcessIdAsync(process.Id, ct);

        // توکن‌های Waiting در این join (که واقعاً به join رسیده‌اند)
        var waiting = allTokens
            .Where(t =>
                t.CurrentElementId == arrivingToken.CurrentElementId &&
                t.State == TokenState.Waiting &&
                t.ScopeId == scopeId)
            .ToList();

        // ArrivedViaFlowId: کلیدهای ورودی که tokens از طریق آن‌ها به join رسیده‌اند
        // این دقیقاً همان چیزی است که BPMN semantics می‌خواهد: Join منتظر tokens است که از incoming flows می‌آیند
        var arrivedKeys = waiting
            .Select(t => t.ArrivedViaFlowId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        _logger.LogInformation(
            "[JOIN] Gateway={Gw} ScopeId={ScopeId} Incoming={InCnt} Expected={Expected} Waiting={WaitingCnt} ArrivedDistinct={ArrivedCnt}",
            arrivingToken.CurrentElementId,
            scopeId,
            incoming.Count,
            expectedCount,
            waiting.Count,
            arrivedKeys.Count);

        // قانون BPMN: Join فقط بر اساس arrivals تصمیم‌گیری می‌کند
        // Join منتظر tokens است که از incoming flows می‌آیند (ArrivedViaFlowId)
        // Incident فقط برای UX/Operations است، نه برای تصمیم‌گیری Join
        if (arrivedKeys.Count < expectedCount)
        {
            // هنوز همه‌ی شاخه‌های مورد انتظار به join نرسیده‌اند
            _logger.LogDebug(
                "[JOIN] Not all expected branches arrived. Waiting. ScopeId={ScopeId} Arrived={Arrived} Expected={Expected}",
                scopeId,
                arrivedKeys.Count,
                expectedCount);
            return true; // Still waiting
        }

        // Merge: اگر حداقل یکی executable باشد، survivor executable انتخاب می‌شود
        var survivor = waiting.FirstOrDefault(t => t.IsExecutable) ?? waiting.First();
        var outputExecutable = waiting.Any(t => t.IsExecutable);

        _logger.LogWarning("[JOIN] MERGE READY. Survivor={Survivor} OutputExecutable={OutExec} WaitingTokens={Tokens}",
            survivor.Id, outputExecutable,
            string.Join(",", waiting.Select(t => $"{t.Id}:{t.ArrivedViaFlowId}:{t.IsExecutable}:{t.State}")));

        // terminate non-survivors
        foreach (var t in waiting)
        {
            if (t.Id == survivor.Id) continue;
            t.Terminate("Merged into survivor at join gateway.");
            process.RemoveToken(t.Id);
        }

        // اگر outputExecutable=false و survivor اجرایی باشد، مشکلی نیست (survivor executable می‌ماند)
        // اگر outputExecutable=true و survivor non-exec باشد، این حالت رخ نمی‌دهد چون survivor را از بین executable ها انتخاب کردیم.

        survivor.ResumeWithoutProcessing();
        survivor.ClearArrivedVia();
        survivor.ClearScope();

        var outFlow = outgoing.Single();
        if (string.IsNullOrWhiteSpace(outFlow.targetRef))
            throw new InvalidOperationException("Merge gateway must have exactly one outgoing with targetRef.");

        survivor.MoveTo(outFlow.targetRef!, FlowKey(outFlow));
        return true;
    }

    private static string FlowKey(BpmnSequenceFlow f)
        => !string.IsNullOrWhiteSpace(f.id) ? f.id! : $"{f.sourceRef}->{f.targetRef}";
}
