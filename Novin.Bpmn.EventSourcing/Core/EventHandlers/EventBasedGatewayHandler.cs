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
/// پردازش‌کننده رویدادهای مرتبط با گیت‌وی مبتنی بر رویداد (Event-Based)
/// </summary>
public class EventBasedGatewayHandler : BaseEventHandler<ElementProcessing>
{
    /// <summary>
    /// ایجاد یک نمونه جدید از پردازش‌کننده گیت‌وی مبتنی بر رویداد
    /// </summary>
    /// <param name="logger">سیستم ثبت وقایع</param>
    /// <param name="stateStore">مخزن وضعیت</param>
    /// <param name="eventBus">گذرگاه رویداد</param>
    public EventBasedGatewayHandler(
        ILogger<EventBasedGatewayHandler> logger,
        IStateStore stateStore,
        IEventBus eventBus)
        : base(logger, stateStore, eventBus)
    {
    }

    /// <inheritdoc />
    protected override async Task ProcessEventAsync(ElementProcessing @event, CancellationToken cancellationToken = default)
    {
        // فقط پردازش المان‌های گیت‌وی مبتنی بر رویداد
        if (@event.ElementType != "bpmn:EventBasedGateway")
        {
            return;
        }

        Logger.LogDebug("Processing event-based gateway for {ElementId} in process {ProcessInstanceId}",
            @event.ElementId, @event.ProcessInstanceId);

        // دریافت وضعیت فعلی فرآیند
        var (state, version) = await StateStore.GetStateWithVersionAsync<BpmnProcessState>(@event.ProcessInstanceId);
        
        if (state == null)
        {
            Logger.LogWarning("Process instance state not found for {ProcessInstanceId}", @event.ProcessInstanceId);
            return;
        }

        // گیت‌وی مبتنی بر رویداد همیشه در حالت Split است و منتظر رویداد می‌ماند
        await HandleEventBasedGatewayAsync(@event, state, version, cancellationToken);
    }

    private async Task HandleEventBasedGatewayAsync(
        ElementProcessing @event,
        BpmnProcessState state,
        long version,
        CancellationToken cancellationToken)
    {
        if (state.GatewayInfo == null || !state.GatewayInfo.TryGetValue(@event.ElementId, out var gatewayInfo))
        {
            Logger.LogWarning("Gateway info not found for gateway {GatewayId}", @event.ElementId);
            return;
        }
        
        // ما الان فقط رویدادهای بعدی را فعال می‌کنیم و منتظر می‌مانیم یکی از آنها رخ دهد
        var eventTargets = new List<string>();
        
        // در یک پیاده‌سازی واقعی، ما باید بررسی کنیم که کدام یک از مسیرهای خروجی به رویدادهای میانی می‌رسد
        foreach (var flowId in gatewayInfo.OutgoingFlows)
        {
            // در اینجا فرض می‌کنیم که شناسه مسیر معادل شناسه رویداد هدف است
            // در پیاده‌سازی واقعی ما باید فلو را دنبال کنیم تا به رویداد برسیم
            eventTargets.Add(flowId);
        }
        
        // اضافه کردن اطلاعات گیت‌وی به وضعیت فرآیند
        if (state.EventBasedGateways == null)
        {
            state.EventBasedGateways = new Dictionary<string, EventBasedGatewayInfo>();
        }
        
        state.EventBasedGateways[@event.ElementId] = new EventBasedGatewayInfo
        {
            GatewayId = @event.ElementId,
            EventTargets = eventTargets,
            IsActive = true,
            ActivatedAt = @event.Timestamp
        };
        
        // ذخیره وضعیت
        await StateStore.SaveStateAsync(@event.ProcessInstanceId, state, version);
    }
}

/// <summary>
/// پردازش‌کننده رویدادهای میانی پس از گیت‌وی مبتنی بر رویداد
/// </summary>
public class IntermediateEventAfterEventBasedGatewayHandler : BaseEventHandler<ElementProcessing>
{
    /// <summary>
    /// ایجاد یک نمونه جدید از پردازش‌کننده رویدادهای میانی پس از گیت‌وی مبتنی بر رویداد
    /// </summary>
    /// <param name="logger">سیستم ثبت وقایع</param>
    /// <param name="stateStore">مخزن وضعیت</param>
    /// <param name="eventBus">گذرگاه رویداد</param>
    public IntermediateEventAfterEventBasedGatewayHandler(
        ILogger<IntermediateEventAfterEventBasedGatewayHandler> logger,
        IStateStore stateStore,
        IEventBus eventBus)
        : base(logger, stateStore, eventBus)
    {
    }

    /// <inheritdoc />
    protected override async Task ProcessEventAsync(ElementProcessing @event, CancellationToken cancellationToken = default)
    {
        // فقط پردازش رویدادهای میانی
        if (!IsIntermediateEvent(@event.ElementType))
        {
            return;
        }

        Logger.LogDebug("Processing intermediate event for {ElementId} in process {ProcessInstanceId}",
            @event.ElementId, @event.ProcessInstanceId);

        // دریافت وضعیت فعلی فرآیند
        var (state, version) = await StateStore.GetStateWithVersionAsync<BpmnProcessState>(@event.ProcessInstanceId);
        
        if (state == null)
        {
            Logger.LogWarning("Process instance state not found for {ProcessInstanceId}", @event.ProcessInstanceId);
            return;
        }

        // بررسی اینکه آیا این رویداد پس از یک گیت‌وی مبتنی بر رویداد فعال شده است
        if (state.EventBasedGateways == null || !IsEventAfterEventBasedGateway(@event.ElementId, state))
        {
            // این رویداد پس از گیت‌وی مبتنی بر رویداد نیست، پس کاری نمی‌کنیم
            return;
        }

        // در این صورت، ما باید بقیه رویدادهای فعال شده پس از گیت‌وی را غیرفعال کنیم
        await HandleIntermediateEventAfterEventBasedGatewayAsync(@event, state, version, cancellationToken);
    }

    private bool IsIntermediateEvent(string elementType)
    {
        // بررسی می‌کنیم که آیا این المان یک رویداد میانی است
        return elementType.Contains("IntermediateCatchEvent") || 
               elementType.Contains("IntermediateThrowEvent") ||
               elementType.Contains("MessageEvent") ||
               elementType.Contains("TimerEvent") ||
               elementType.Contains("SignalEvent") ||
               elementType.Contains("ConditionalEvent");
    }

    private bool IsEventAfterEventBasedGateway(string eventId, BpmnProcessState state)
    {
        if (state.EventBasedGateways == null)
        {
            return false;
        }
        
        // بررسی می‌کنیم که آیا این رویداد جزو رویدادهای فعال شده پس از یک گیت‌وی مبتنی بر رویداد است
        foreach (var gateway in state.EventBasedGateways.Values)
        {
            if (gateway.IsActive && gateway.EventTargets.Contains(eventId))
            {
                return true;
            }
        }
        
        return false;
    }

    private async Task HandleIntermediateEventAfterEventBasedGatewayAsync(
        ElementProcessing @event,
        BpmnProcessState state,
        long version,
        CancellationToken cancellationToken)
    {
        // یافتن گیت‌وی مبتنی بر رویداد که این رویداد را فعال کرده است
        string gatewayId = null;
        foreach (var gateway in state.EventBasedGateways.Values)
        {
            if (gateway.IsActive && gateway.EventTargets.Contains(@event.ElementId))
            {
                gatewayId = gateway.GatewayId;
                break;
            }
        }
        
        if (gatewayId == null)
        {
            Logger.LogWarning("Could not find event-based gateway for event {EventId}", @event.ElementId);
            return;
        }
        
        // تمام رویدادهای دیگر فعال شده پس از این گیت‌وی را غیرفعال می‌کنیم
        var eventInfo = state.EventBasedGateways[gatewayId];
        eventInfo.IsActive = false;
        eventInfo.SelectedEventId = @event.ElementId;
        
        // ذخیره وضعیت
        await StateStore.SaveStateAsync(@event.ProcessInstanceId, state, version);
        
        // انتشار رویداد خاتمه برای سایر رویدادها
        foreach (var targetId in eventInfo.EventTargets)
        {
            if (targetId != @event.ElementId)
            {
                await EventBus.PublishAsync(new ElementTerminated
                {
                    ProcessInstanceId = @event.ProcessInstanceId,
                    ElementId = targetId,
                    ElementType = "bpmn:IntermediateCatchEvent" // این یک مقدار پیش‌فرض است
                }, cancellationToken);
            }
        }
    }
} 