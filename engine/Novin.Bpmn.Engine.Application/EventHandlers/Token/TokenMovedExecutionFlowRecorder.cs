using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.Events;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

public sealed class TokenMovedExecutionFlowRecorder : INotificationHandler<TokenMovedEvent>
{
    private readonly IUnitOfWork _uow;
    private readonly IExecutionFlowRepository _flows;
    private readonly ILogger<TokenMovedExecutionFlowRecorder> _logger;

    public TokenMovedExecutionFlowRecorder(
        IUnitOfWork uow,
        IExecutionFlowRepository flows,
        ILogger<TokenMovedExecutionFlowRecorder> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _flows = flows ?? throw new ArgumentNullException(nameof(flows));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(TokenMovedEvent e, CancellationToken ct)
    {
        

        await _uow.ExecuteInTransactionAsync(async trxCt =>
        {
            
            var via = e.ViaFlowIds ?? [];
            var key = ExecutionFlowRecord.BuildEventKey(
                processId: e.ProcessId,
                tokenId: e.TokenId,
                fromElementId: e.FromElementId ?? "",
                toElementId: e.ToElementId ?? "",
                viaFlowIds: via,
                occurredAtUtcUtc: e.OccurredAtUtc == default ? DateTime.UtcNow : e.OccurredAtUtc,
                scopeId: e.ScopeId,
                activityInstanceId: e.ActivityInstanceId);

            // ✅ idempotency guard BEFORE allocating Position (prevents gaps on retries)
            if (await _flows.ExistsByEventKeyAsync(key, trxCt))
                return;

            var pos = await _flows.GetNextPositionAsync(e.ProcessId, trxCt);

            var rec = ExecutionFlowRecord.Create(
                processId: e.ProcessId,
                tokenId: e.TokenId,
                position: pos,
                fromElementId: e.FromElementId ?? "",
                toElementId: e.ToElementId ?? "",
                viaFlowIds: via,
                occurredAtUtc: e.OccurredAtUtc,
                scopeId: e.ScopeId,
                activityInstanceId: e.ActivityInstanceId);

            await _flows.AddAsync(rec, trxCt);

            _logger.LogDebug(
                "[EXEC-FLOW] Added. Proc={Proc} Pos={Pos} Token={Token} {From}->{To} Key={Key}",
                e.ProcessId, pos, e.TokenId, e.FromElementId, e.ToElementId, rec.EventKey);

        }, ct);

   
    }
}
