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
/// پردازش‌کننده رویدادهای مرتبط با گیت‌وی انحصاری (XOR)
/// </summary>
public class ExclusiveGatewayHandler : BaseEventHandler<ElementActivated>
{
    /// <summary>
    /// ایجاد یک نمونه جدید از پردازش‌کننده گیت‌وی انحصاری
    /// </summary>
    /// <param name="logger">سیستم ثبت وقایع</param>
    /// <param name="stateStore">مخزن وضعیت</param>
    /// <param name="eventBus">گذرگاه رویداد</param>
    public ExclusiveGatewayHandler(
        ILogger<ExclusiveGatewayHandler> logger,
        IStateStore stateStore,
        IEventBus eventBus)
        : base(logger, stateStore, eventBus)
    {
    }

    /// <inheritdoc />
    protected override async Task ProcessEventAsync(ElementActivated @event, CancellationToken cancellationToken = default)
    {
        // فقط پردازش المان‌های گیت‌وی انحصاری
        if (@event.ElementType != "bpmn:ExclusiveGateway")
        {
            return;
        }

        Logger.LogDebug("Processing exclusive gateway activation for {ElementId} in process {ProcessInstanceId}",
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
            // گیت‌وی در حالت Split است - ارزیابی شرط‌ها و فعال کردن دقیقاً یک مسیر
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
        // در حالت مرج (Join)، گیت‌وی انحصاری به محض رسیدن اولین توکن، می‌تواند تکمیل شود
        // بنابراین، اگر ما به اینجا رسیده‌ایم، یعنی حداقل یک توکن رسیده است و می‌توانیم ادامه دهیم
        
        if (state.GatewayInfo == null || !state.GatewayInfo.TryGetValue(@event.ElementId, out var gatewayInfo))
        {
            Logger.LogWarning("Gateway info not found for gateway {GatewayId}", @event.ElementId);
            return;
        }
        
        // ذخیره وضعیت
        await StateStore.SaveStateAsync(@event.ProcessInstanceId, state, version);
        
        // فوراً رویداد تکمیل را منتشر می‌کنیم
        await EventBus.PublishAsync(new ElementCompleting
        {
            ProcessInstanceId = @event.ProcessInstanceId,
            ElementId = @event.ElementId,
            ElementType = @event.ElementType,
            OutgoingFlowIds = gatewayInfo.OutgoingFlows
        }, cancellationToken);
    }

    private async Task HandleSplitGatewayAsync(
        ElementActivated @event,
        BpmnProcessState state,
        long version,
        CancellationToken cancellationToken)
    {
        // در حالت Split، گیت‌وی انحصاری دقیقاً یک مسیر را فعال می‌کند
        if (state.GatewayInfo == null || !state.GatewayInfo.TryGetValue(@event.ElementId, out var gatewayInfo))
        {
            Logger.LogWarning("Gateway info not found for gateway {GatewayId}", @event.ElementId);
            return;
        }
        
        // ارزیابی شرط‌ها برای هر مسیر خروجی و انتخاب اولین مسیر که شرط آن برقرار است
        string selectedFlow = null;
        
        foreach (var flowId in gatewayInfo.OutgoingFlows)
        {
            // در یک پیاده‌سازی واقعی، ما باید شرط مربوط به هر مسیر را ارزیابی کنیم
            if (EvaluateCondition(flowId, state))
            {
                selectedFlow = flowId;
                break; // به محض یافتن اولین مسیر، حلقه را ترک می‌کنیم
            }
        }
        
        // اگر هیچ مسیری انتخاب نشد، مسیر پیش‌فرض را انتخاب می‌کنیم
        if (selectedFlow == null && gatewayInfo.OutgoingFlows.Count > 0)
        {
            selectedFlow = GetDefaultFlow(gatewayInfo);
        }
        
        if (selectedFlow == null)
        {
            Logger.LogError("No flow selected for exclusive gateway {GatewayId}", @event.ElementId);
            return;
        }
        
        // ذخیره وضعیت
        await StateStore.SaveStateAsync(@event.ProcessInstanceId, state, version);
        
        // انتشار رویداد تکمیل گیت‌وی با مسیر انتخاب شده
        await EventBus.PublishAsync(new ElementCompleting
        {
            ProcessInstanceId = @event.ProcessInstanceId,
            ElementId = @event.ElementId,
            ElementType = @event.ElementType,
            OutgoingFlowIds = new List<string> { selectedFlow }
        }, cancellationToken);
    }

    private bool EvaluateCondition(string flowId, BpmnProcessState state)
    {
        // در یک پیاده‌سازی واقعی، ما باید شرط مربوط به این مسیر را ارزیابی کنیم
        // این می‌تواند شامل ارزیابی عبارات و متغیرها باشد
        
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