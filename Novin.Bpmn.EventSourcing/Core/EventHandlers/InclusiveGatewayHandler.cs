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
/// پردازش‌کننده رویدادهای مرتبط با گیت‌وی فراگیر (OR)
/// </summary>
public class InclusiveGatewayHandler : BaseEventHandler<ElementActivated>
{
    /// <summary>
    /// ایجاد یک نمونه جدید از پردازش‌کننده گیت‌وی فراگیر
    /// </summary>
    /// <param name="logger">سیستم ثبت وقایع</param>
    /// <param name="stateStore">مخزن وضعیت</param>
    /// <param name="eventBus">گذرگاه رویداد</param>
    public InclusiveGatewayHandler(
        ILogger<InclusiveGatewayHandler> logger,
        IStateStore stateStore,
        IEventBus eventBus)
        : base(logger, stateStore, eventBus)
    {
    }

    /// <inheritdoc />
    protected override async Task ProcessEventAsync(ElementActivated @event, CancellationToken cancellationToken = default)
    {
        // فقط پردازش المان‌های گیت‌وی فراگیر
        if (@event.ElementType != "bpmn:InclusiveGateway")
        {
            return;
        }

        Logger.LogDebug("Processing inclusive gateway activation for {ElementId} in process {ProcessInstanceId}",
            @event.ElementId, @event.ProcessInstanceId);

        // دریافت وضعیت فعلی فرآیند
        var (state, version) = await StateStore.GetStateWithVersionAsync<BpmnProcessState>(@event.ProcessInstanceId);
        
        if (state == null)
        {
            Logger.LogWarning("Process instance state not found for {ProcessInstanceId}", @event.ProcessInstanceId);
            return;
        }

        // بررسی اینکه آیا این گیت‌وی در حالت مرج (Join) است
        if (IsJoinGateway(@event.ElementId, state))
        {
            await HandleJoinGatewayAsync(@event, state, version, cancellationToken);
        }
        else
        {
            // گیت‌وی در حالت Split است - ارزیابی شرط‌ها و فعال کردن مسیرهای خروجی
            await HandleSplitGatewayAsync(@event, state, version, cancellationToken);
        }
    }

    private bool IsJoinGateway(string gatewayId, BpmnProcessState state)
    {
        // در یک پیاده‌سازی واقعی، ما باید به مدل BPMN مراجعه کنیم
        if (state.GatewayInfo != null && state.GatewayInfo.TryGetValue(gatewayId, out var info))
        {
            return info.IncomingFlows.Count > 1;
        }
        
        return false;
    }

    private async Task HandleJoinGatewayAsync(
        ElementActivated @event,
        BpmnProcessState state,
        long version,
        CancellationToken cancellationToken)
    {
        // در حالت مرج (Join)، گیت‌وی فراگیر باید منتظر همهٔ مسیرهای فعال باشد
        if (state.GatewayInfo == null || !state.GatewayInfo.TryGetValue(@event.ElementId, out var gatewayInfo))
        {
            Logger.LogWarning("Gateway info not found for gateway {GatewayId}", @event.ElementId);
            return;
        }
        
        // نکته مهم: گیت‌وی فراگیر فقط منتظر مسیرهای فعال است، نه همهٔ مسیرها
        
        // مرحله 1: شناسایی مسیرهای فعال (در یک پیاده‌سازی واقعی، این اطلاعات را از مدل دریافت می‌کنیم)
        var activeIncomingFlows = gatewayInfo.IncomingFlows
            .Where(flowId => IsFlowActive(flowId, state))
            .ToList();
        
        // مرحله 2: بررسی اینکه آیا همهٔ مسیرهای فعال رسیده‌اند
        var completedActiveFlows = activeIncomingFlows
            .Where(flowId => state.CompletedElements.Contains(flowId))
            .Count();
        
        // اگر همهٔ مسیرهای فعال رسیده‌اند، گیت‌وی می‌تواند تکمیل شود
        if (completedActiveFlows >= activeIncomingFlows.Count && activeIncomingFlows.Count > 0)
        {
            // تکمیل گیت‌وی فراگیر
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
            // هنوز منتظر سایر مسیرهای فعال هستیم
            await StateStore.SaveStateAsync(@event.ProcessInstanceId, state, version);
        }
    }

    private bool IsFlowActive(string flowId, BpmnProcessState state)
    {
        // در یک پیاده‌سازی واقعی، باید بررسی کنیم آیا این مسیر فعال شده است
        // این می‌تواند شامل ارزیابی شرط‌ها و بررسی تاریخچه رویدادها باشد
        
        // برای سادگی، فرض می‌کنیم مسیر فعال است اگر:
        // 1. مسیر قبلاً تکمیل شده است، یا
        // 2. مسیر در مسیرهای فعال است
        return state.CompletedElements.Contains(flowId) || state.ActiveElements.Contains(flowId);
    }

    private async Task HandleSplitGatewayAsync(
        ElementActivated @event,
        BpmnProcessState state,
        long version,
        CancellationToken cancellationToken)
    {
        // در حالت Split، گیت‌وی فراگیر مسیرهایی را فعال می‌کند که شرط آنها برقرار است
        if (state.GatewayInfo == null || !state.GatewayInfo.TryGetValue(@event.ElementId, out var gatewayInfo))
        {
            Logger.LogWarning("Gateway info not found for gateway {GatewayId}", @event.ElementId);
            return;
        }
        
        // ارزیابی شرط‌ها برای هر مسیر خروجی
        var activeOutgoingFlows = new List<string>();
        
        foreach (var flowId in gatewayInfo.OutgoingFlows)
        {
            // در یک پیاده‌سازی واقعی، ما باید شرط‌ها را ارزیابی کنیم
            // برای سادگی، فرض می‌کنیم همهٔ مسیرها فعال هستند
            if (EvaluateCondition(flowId, state))
            {
                activeOutgoingFlows.Add(flowId);
            }
        }
        
        // اگر هیچ مسیری فعال نباشد، مسیر پیش‌فرض را فعال می‌کنیم (اگر وجود داشته باشد)
        if (activeOutgoingFlows.Count == 0 && gatewayInfo.OutgoingFlows.Count > 0)
        {
            var defaultFlow = GetDefaultFlow(gatewayInfo);
            if (defaultFlow != null)
            {
                activeOutgoingFlows.Add(defaultFlow);
            }
        }
        
        // ذخیره وضعیت
        await StateStore.SaveStateAsync(@event.ProcessInstanceId, state, version);
        
        // انتشار رویداد تکمیل گیت‌وی با مسیرهای فعال
        await EventBus.PublishAsync(new ElementCompleting
        {
            ProcessInstanceId = @event.ProcessInstanceId,
            ElementId = @event.ElementId,
            ElementType = @event.ElementType,
            OutgoingFlowIds = activeOutgoingFlows
        }, cancellationToken);
    }

    private bool EvaluateCondition(string flowId, BpmnProcessState state)
    {
        // در یک پیاده‌سازی واقعی، ما باید شرط مربوط به این مسیر را ارزیابی کنیم
        // این می‌تواند شامل ارزیابی عبارات‌ و متغیرها باشد
        
        // برای سادگی، فرض می‌کنیم شرط برقرار است
        return true;
    }

    private string GetDefaultFlow(GatewayInfo gatewayInfo)
    {
        // در یک پیاده‌سازی واقعی، ما باید مسیر پیش‌فرض را از مدل BPMN بخوانیم
        
        // برای سادگی، اولین مسیر را به عنوان پیش‌فرض در نظر می‌گیریم
        return gatewayInfo.OutgoingFlows.FirstOrDefault();
    }
} 