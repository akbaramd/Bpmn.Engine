using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Core.Models;
using Novin.Bpmn.EventSourcing.Events;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Novin.Bpmn.EventSourcing.Core.EventHandlers;

/// <summary>
/// پردازش‌کننده رویدادهای مرتبط با گیت‌وی موازی
/// </summary>
public class ParallelGatewayHandler : BaseEventHandler<ElementActivated>
{
    /// <summary>
    /// ایجاد یک نمونه جدید از پردازش‌کننده گیت‌وی موازی
    /// </summary>
    /// <param name="logger">سیستم ثبت وقایع</param>
    /// <param name="stateStore">مخزن وضعیت</param>
    /// <param name="eventBus">گذرگاه رویداد</param>
    public ParallelGatewayHandler(
        ILogger<ParallelGatewayHandler> logger,
        IStateStore stateStore,
        IEventBus eventBus)
        : base(logger, stateStore, eventBus)
    {
    }

    /// <inheritdoc />
    protected override async Task ProcessEventAsync(ElementActivated @event, CancellationToken cancellationToken = default)
    {
        // فقط پردازش المان‌های گیت‌وی موازی
        if (@event.ElementType != "bpmn:ParallelGateway")
        {
            return;
        }

        Logger.LogDebug("Processing parallel gateway activation for {ElementId} in process {ProcessInstanceId}",
            @event.ElementId, @event.ProcessInstanceId);

        // دریافت وضعیت فعلی فرآیند
        var (state, version) = await StateStore.GetStateWithVersionAsync<BpmnProcessState>(@event.ProcessInstanceId);
        
        if (state == null)
        {
            Logger.LogWarning("Process instance state not found for {ProcessInstanceId}", @event.ProcessInstanceId);
            return;
        }

        // در یک پیاده‌سازی واقعی، ما باید از تعریف BPMN استفاده کنیم تا تعداد جریان‌های ورودی به گیت‌وی را بررسی کنیم
        // برای مثال، فرض می‌کنیم ما این اطلاعات را در متغیر ProcessState.GatewayInfo ذخیره کرده‌ایم
        
        // بررسی اینکه آیا این گیت‌وی در حالت مرج (Join) است
        if (IsJoinGateway(@event.ElementId, state))
        {
            await HandleJoinGatewayAsync(@event, state, version, cancellationToken);
        }
        else
        {
            // گیت‌وی در حالت Split است - فعال کردن تمام مسیرهای خروجی
            await HandleSplitGatewayAsync(@event, state, version, cancellationToken);
        }
    }

    private bool IsJoinGateway(string gatewayId, BpmnProcessState state)
    {
        // در یک پیاده‌سازی واقعی، ما باید به مدل BPMN مراجعه کنیم
        // برای مثال، می‌توانیم بررسی کنیم که آیا گیت‌وی بیش از یک جریان ورودی دارد
        
        // برای این مثال، فرض می‌کنیم یک دیکشنری از اطلاعات گیت‌وی در وضعیت ذخیره شده است
        if (state.GatewayInfo != null && state.GatewayInfo.TryGetValue(gatewayId, out var info))
        {
            return info.IncomingFlows.Count > 1;
        }
        
        // اگر اطلاعاتی موجود نباشد، فرض می‌کنیم این یک گیت‌وی Split است
        return false;
    }

    private async Task HandleJoinGatewayAsync(
        ElementActivated @event,
        BpmnProcessState state,
        long version,
        CancellationToken cancellationToken)
    {
        // در حالت مرج (Join)، ما باید بررسی کنیم آیا از تمام مسیرهای ورودی توکن رسیده است
        
        if (state.GatewayInfo == null || !state.GatewayInfo.TryGetValue(@event.ElementId, out var gatewayInfo))
        {
            Logger.LogWarning("Gateway info not found for gateway {GatewayId}", @event.ElementId);
            return;
        }
        
        // گیت‌وی موازی باید منتظر باشد تا تمام مسیرهای ورودی فعال شوند
        
        // بررسی اینکه چه تعداد از مسیرهای ورودی در المان‌های تکمیل شده هستند
        var completedIncomingFlows = gatewayInfo.IncomingFlows
            .Where(flowId => state.CompletedElements.Contains(flowId))
            .Count();
            
        // اگر تمام مسیرهای ورودی رسیده‌اند، گیت‌وی می‌تواند تکمیل شود
        if (completedIncomingFlows >= gatewayInfo.IncomingFlows.Count)
        {
            // تکمیل گیت‌وی موازی
            await StateStore.SaveStateAsync(@event.ProcessInstanceId, state, version);
            
            // انتشار رویداد تکمیل گیت‌وی
            await EventBus.PublishAsync(new ElementCompleting
            {
                ProcessInstanceId = @event.ProcessInstanceId,
                ElementId = @event.ElementId,
                ElementType = @event.ElementType,
                OutgoingFlowIds = gatewayInfo.OutgoingFlows
            }, cancellationToken);
        }
        else
        {
            // هنوز منتظر سایر مسیرهای ورودی هستیم
            await StateStore.SaveStateAsync(@event.ProcessInstanceId, state, version);
        }
    }

    private async Task HandleSplitGatewayAsync(
        ElementActivated @event,
        BpmnProcessState state,
        long version,
        CancellationToken cancellationToken)
    {
        // در حالت Split، گیت‌وی موازی تمام مسیرهای خروجی را فعال می‌کند
        
        if (state.GatewayInfo == null || !state.GatewayInfo.TryGetValue(@event.ElementId, out var gatewayInfo))
        {
            Logger.LogWarning("Gateway info not found for gateway {GatewayId}", @event.ElementId);
            return;
        }
        
        // ذخیره وضعیت
        await StateStore.SaveStateAsync(@event.ProcessInstanceId, state, version);
        
        // انتشار رویداد تکمیل گیت‌وی با تمام مسیرهای خروجی
        await EventBus.PublishAsync(new ElementCompleting
        {
            ProcessInstanceId = @event.ProcessInstanceId,
            ElementId = @event.ElementId,
            ElementType = @event.ElementType,
            OutgoingFlowIds = gatewayInfo.OutgoingFlows
        }, cancellationToken);
    }
}