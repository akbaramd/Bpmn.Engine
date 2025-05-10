using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Events;
using Novin.Bpmn.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Novin.Bpmn.EventSourcing.Core.EventHandlers;

/// <summary>
/// پردازش‌کننده رویداد تکمیل المان
/// این پردازش‌کننده مسئول ادامه جریان فرآیند پس از تکمیل یک المان است
/// </summary>
public class ElementCompletedHandler : IBpmnEventHandler<ElementCompleted>
{
    private readonly ILogger<ElementCompletedHandler> _logger;
    private readonly IStateStore _stateStore;
    private readonly IEventBus _eventBus;
    private readonly IBpmnDefinitionStorage _definitionStorage;
    
    /// <summary>
    /// ایجاد یک نمونه جدید از پردازش‌کننده رویداد تکمیل المان
    /// </summary>
    /// <param name="logger">سیستم ثبت وقایع</param>
    /// <param name="stateStore">مخزن وضعیت</param>
    /// <param name="eventBus">گذرگاه رویداد</param>
    /// <param name="definitionStorage">مخزن تعاریف BPMN</param>
    public ElementCompletedHandler(
        ILogger<ElementCompletedHandler> logger,
        IStateStore stateStore,
        IEventBus eventBus,
        IBpmnDefinitionStorage definitionStorage)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _definitionStorage = definitionStorage ?? throw new ArgumentNullException(nameof(definitionStorage));
    }
    
    /// <inheritdoc />
    public async Task HandleAsync(ElementCompleted @event, CancellationToken cancellationToken = default)
    {
        if (@event == null)
        {
            throw new ArgumentNullException(nameof(@event));
        }
        
        try
        {
            Console.WriteLine($"✅ ElementCompletedHandler.HandleAsync called for element {@event.ElementId}");
            
            _logger.LogDebug("Processing ElementCompleted event for element {ElementId} in process {ProcessInstanceId}", 
                @event.ElementId, @event.ProcessInstanceId);
            
            // ابتدا وضعیت فرآیند را بازیابی می‌کنیم
            var (state, version) = await _stateStore.GetStateWithVersionAsync<BpmnProcessState>(@event.ProcessInstanceId);
            
            if (state == null)
            {
                _logger.LogWarning("Process instance state not found for {ProcessInstanceId}", @event.ProcessInstanceId);
                return;
            }
            
            // بررسی نوع المان تکمیل شده و المان‌های بعدی که باید فعال شوند
            // در حالت‌های مختلف، منطق ادامه فرآیند متفاوت است
            
            switch (@event.ElementType)
            {
                case "bpmn:StartEvent":
                    // برای رویداد شروع، باید المان بعدی را فعال کنیم
                    await HandleStartEventCompletionAsync(state, @event, cancellationToken);
                    break;
                    
                case "bpmn:EndEvent":
                    // برای رویداد پایان، باید فرآیند را تکمیل کنیم
                    await HandleEndEventCompletionAsync(state, @event, cancellationToken);
                    break;
                    
                case "bpmn:UserTask":
                    // برای وظیفه کاربر، باید المان بعدی را فعال کنیم
                    await HandleUserTaskCompletionAsync(state, @event, cancellationToken);
                    break;
                    
                case "bpmn:ServiceTask":
                    // برای وظیفه سرویس، باید المان بعدی را فعال کنیم
                    await HandleServiceTaskCompletionAsync(state, @event, cancellationToken);
                    break;
                    
                case "bpmn:ParallelGateway":
                    // برای دروازه موازی، باید منطق انشعاب/ادغام را پیاده‌سازی کنیم
                    await HandleParallelGatewayCompletionAsync(state, @event, cancellationToken);
                    break;
                    
                case "bpmn:InclusiveGateway":
                    // برای دروازه فراگیر، باید منطق انشعاب/ادغام را پیاده‌سازی کنیم
                    await HandleInclusiveGatewayCompletionAsync(state, @event, cancellationToken);
                    break;
                    
                case "bpmn:ExclusiveGateway":
                    // برای دروازه انحصاری، باید منطق انشعاب/ادغام را پیاده‌سازی کنیم
                    await HandleExclusiveGatewayCompletionAsync(state, @event, cancellationToken);
                    break;
                    
                case "bpmn:BoundaryEvent":
                    // برای رویداد مرزی، بررسی می‌کنیم که آیا قطع‌کننده است یا خیر
                    await HandleBoundaryEventTriggerAsync(state, @event, cancellationToken);
                    break;
                    
                default:
                    _logger.LogDebug("Element {ElementId} of type {ElementType} completed in process {ProcessInstanceId}", 
                        @event.ElementId, @event.ElementType, @event.ProcessInstanceId);
                    // فعال‌سازی المان‌های بعدی بر اساس جریان‌های خروجی
                    await ActivateNextElementsAsync(state, @event, cancellationToken);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling ElementCompleted event for element {ElementId} in process {ProcessInstanceId}",
                @event.ElementId, @event.ProcessInstanceId);
            throw; // رخداد را رد می‌کنیم تا سیستم مدیریت رخداد آن را مدیریت کند
        }
    }
    
    /// <summary>
    /// پردازش تکمیل رویداد شروع
    /// </summary>
    private async Task HandleStartEventCompletionAsync(
        BpmnProcessState state, 
        ElementCompleted @event,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing completion of start event {EventId} in process {ProcessInstanceId}",
            @event.ElementId, @event.ProcessInstanceId);
            
        // برای رویداد شروع، باید المان‌های بعدی را فعال کنیم
        await ActivateNextElementsAsync(state, @event, cancellationToken);
    }
    
    /// <summary>
    /// پردازش تکمیل رویداد پایان
    /// </summary>
    private async Task HandleEndEventCompletionAsync(
        BpmnProcessState state, 
        ElementCompleted @event,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing completion of end event {EventId} in process {ProcessInstanceId}",
            @event.ElementId, @event.ProcessInstanceId);
            
        // رویداد پایان به معنی تکمیل فرآیند است
        // باید رویداد تکمیل فرآیند را منتشر کنیم
        
        await _eventBus.PublishAsync(new ProcessInstanceCompleting
        {
            ProcessInstanceId = @event.ProcessInstanceId,
            FinalVariables = state.Variables,
            EndEventId = @event.ElementId
        }, cancellationToken);
        
        // کمی صبر برای پردازش رویداد
        await Task.Delay(50, cancellationToken);
        
        await _eventBus.PublishAsync(new ProcessCompletedEvent
        {
            ProcessInstanceId = @event.ProcessInstanceId,
            EndEventId = @event.ElementId
        }, cancellationToken);
        
        _logger.LogInformation("Process {ProcessInstanceId} completed with end event {EndEventId}", 
            @event.ProcessInstanceId, @event.ElementId);
    }
    
    /// <summary>
    /// پردازش تکمیل وظیفه کاربر
    /// </summary>
    private async Task HandleUserTaskCompletionAsync(
        BpmnProcessState state, 
        ElementCompleted @event,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing completion of user task {TaskId} in process {ProcessInstanceId}",
            @event.ElementId, @event.ProcessInstanceId);
            
        // برای وظیفه کاربر، باید المان بعدی را فعال کنیم
        await ActivateNextElementsAsync(state, @event, cancellationToken);
    }
    
    /// <summary>
    /// پردازش تکمیل وظیفه سرویس
    /// </summary>
    private async Task HandleServiceTaskCompletionAsync(
        BpmnProcessState state, 
        ElementCompleted @event,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing completion of service task {TaskId} in process {ProcessInstanceId}",
            @event.ElementId, @event.ProcessInstanceId);
            
        // برای وظیفه سرویس، باید المان بعدی را فعال کنیم
        await ActivateNextElementsAsync(state, @event, cancellationToken);
    }
    
    /// <summary>
    /// پردازش تکمیل دروازه موازی (AND-Gateway)
    /// </summary>
    private async Task HandleParallelGatewayCompletionAsync(
        BpmnProcessState state,
        ElementCompleted @event,
        CancellationToken cancellationToken)
    {
        // دریافت اطلاعات دروازه از تعریف BPMN
        var gatewayInfo = await GetGatewayInfoAsync(state, @event.ElementId, cancellationToken);
        
        if (gatewayInfo.IsJoin)
        {
            // منطق ادغام دروازه موازی (Join):
            // باید همه توکن‌ها از تمام مسیرهای ورودی دریافت شوند
            
            // آیا این جریان فعلی، آخرین جریان مورد نیاز برای ادغام است؟
            bool canMerge = await CanMergeParallelGatewayAsync(state, @event, gatewayInfo, cancellationToken);
            
            if (canMerge)
            {
                _logger.LogDebug("Parallel gateway {GatewayId} has received all required tokens. Proceeding with merge in process {ProcessInstanceId}",
                    @event.ElementId, @event.ProcessInstanceId);
                
                // تمام شرایط ادغام برآورده شده است، ادامه به المان‌های بعدی
                await ActivateNextElementsAsync(state, @event, cancellationToken);
            }
            else
            {
                _logger.LogDebug("Parallel gateway {GatewayId} is waiting for more tokens to merge in process {ProcessInstanceId}",
                    @event.ElementId, @event.ProcessInstanceId);
                
                // ذخیره وضعیت این توکن در دروازه
                // در پیاده‌سازی واقعی، باید این توکن را در وضعیت دروازه ذخیره کرد
                // و منتظر رسیدن توکن‌های دیگر ماند
                
                // در این نمونه، وضعیت توکن در ActiveElements نگه داشته می‌شود
            }
        }
        else
        {
            // منطق انشعاب دروازه موازی (Fork/Split):
            // همه مسیرهای خروجی باید فعال شوند
            _logger.LogDebug("Parallel gateway {GatewayId} is forking execution in process {ProcessInstanceId}",
                @event.ElementId, @event.ProcessInstanceId);
            
            await ActivateAllOutgoingFlowsAsync(state, @event, gatewayInfo, cancellationToken);
        }
    }
    
    /// <summary>
    /// پردازش تکمیل دروازه فراگیر (OR-Gateway)
    /// </summary>
    private async Task HandleInclusiveGatewayCompletionAsync(
        BpmnProcessState state,
        ElementCompleted @event,
        CancellationToken cancellationToken)
    {
        // دریافت اطلاعات دروازه از تعریف BPMN
        var gatewayInfo = await GetGatewayInfoAsync(state, @event.ElementId, cancellationToken);
        
        if (gatewayInfo.IsJoin)
        {
            // منطق ادغام دروازه فراگیر (Join):
            // باید توکن‌ها از تمام مسیرهای ورودی فعال دریافت شوند
            
            // بررسی وضعیت ادغام
            var canMerge = await CanMergeInclusiveGatewayAsync(state, @event, gatewayInfo, cancellationToken);
            
            if (canMerge)
            {
                _logger.LogDebug("Inclusive gateway {GatewayId} has received all required tokens from active paths. Proceeding with merge in process {ProcessInstanceId}",
                    @event.ElementId, @event.ProcessInstanceId);
                
                // تمام شرایط ادغام برآورده شده است، ادامه به المان‌های بعدی
                await ActivateNextElementsAsync(state, @event, cancellationToken);
            }
            else
            {
                _logger.LogDebug("Inclusive gateway {GatewayId} is waiting for more tokens from active paths in process {ProcessInstanceId}",
                    @event.ElementId, @event.ProcessInstanceId);
                
                // در حالت واقعی، وضعیت این توکن را ذخیره می‌کنیم و منتظر می‌مانیم
            }
        }
        else
        {
            // منطق انشعاب دروازه فراگیر (Fork/Split):
            // مسیرهای خروجی که شرط آنها برقرار است باید فعال شوند
            _logger.LogDebug("Inclusive gateway {GatewayId} is evaluating conditions for outgoing paths in process {ProcessInstanceId}",
                @event.ElementId, @event.ProcessInstanceId);
            
            await ActivateConditionalOutgoingFlowsAsync(state, @event, gatewayInfo, true, cancellationToken);
        }
    }
    
    /// <summary>
    /// پردازش تکمیل دروازه انحصاری (XOR-Gateway)
    /// </summary>
    private async Task HandleExclusiveGatewayCompletionAsync(
        BpmnProcessState state,
        ElementCompleted @event,
        CancellationToken cancellationToken)
    {
        // دریافت اطلاعات دروازه از تعریف BPMN
        var gatewayInfo = await GetGatewayInfoAsync(state, @event.ElementId, cancellationToken);
        
        if (gatewayInfo.IsJoin)
        {
            // منطق ادغام دروازه انحصاری (Join):
            // نیازی به انتظار برای توکن‌های دیگر نیست، اولین توکن رسیده کافی است
            _logger.LogDebug("Exclusive gateway {GatewayId} received a token. As an XOR-join, proceeding immediately in process {ProcessInstanceId}",
                @event.ElementId, @event.ProcessInstanceId);
            
            // ادامه به المان‌های بعدی - بدون نیاز به بررسی سایر توکن‌ها
            await ActivateNextElementsAsync(state, @event, cancellationToken);
        }
        else
        {
            // منطق انشعاب دروازه انحصاری (Fork/Split):
            // فقط اولین مسیر خروجی که شرط آن برقرار است باید فعال شود
            _logger.LogDebug("Exclusive gateway {GatewayId} is evaluating conditions to select exactly one path in process {ProcessInstanceId}",
                @event.ElementId, @event.ProcessInstanceId);
            
            await ActivateConditionalOutgoingFlowsAsync(state, @event, gatewayInfo, false, cancellationToken);
        }
    }
    
    /// <summary>
    /// فعال‌سازی المان‌های بعدی بر اساس جریان‌های خروجی
    /// </summary>
    private async Task ActivateNextElementsAsync(
        BpmnProcessState state,
        ElementCompleted @event,
        CancellationToken cancellationToken)
    {
        // دریافت جریان‌های خروجی از المان فعلی
        var outgoingFlows = await GetOutgoingFlowsAsync(state, @event.ElementId, cancellationToken);
        
        if (outgoingFlows == null || !outgoingFlows.Any())
        {
            _logger.LogDebug("No outgoing flows found for element {ElementId} in process {ProcessInstanceId}",
                @event.ElementId, @event.ProcessInstanceId);
            return;
        }
        
        // فعال‌سازی هر المان بعدی
        foreach (var flow in outgoingFlows)
        {
            await ActivateElementAsync(state, flow.TargetElementId, flow.TargetElementType, 
                @event.ElementId, flow.Id, cancellationToken);
        }
    }
    
    /// <summary>
    /// فعال‌سازی همه مسیرهای خروجی (برای دروازه موازی)
    /// </summary>
    private async Task ActivateAllOutgoingFlowsAsync(
        BpmnProcessState state,
        ElementCompleted @event,
        GatewayInfo gatewayInfo,
        CancellationToken cancellationToken)
    {
        // برای دروازه موازی، تمام مسیرهای خروجی باید فعال شوند
        var outgoingFlows = await GetOutgoingFlowsAsync(state, @event.ElementId, cancellationToken);
        
        if (outgoingFlows == null || !outgoingFlows.Any())
        {
            _logger.LogDebug("No outgoing flows found for parallel gateway {GatewayId} in process {ProcessInstanceId}",
                @event.ElementId, @event.ProcessInstanceId);
            return;
        }
        
        _logger.LogDebug("Activating all {FlowCount} outgoing flows for parallel gateway {GatewayId} in process {ProcessInstanceId}",
            outgoingFlows.Count, @event.ElementId, @event.ProcessInstanceId);
            
        // فعال‌سازی همه مسیرهای خروجی
        foreach (var flow in outgoingFlows)
        {
            await ActivateElementAsync(state, flow.TargetElementId, flow.TargetElementType, 
                @event.ElementId, flow.Id, cancellationToken);
        }
    }
    
    /// <summary>
    /// فعال‌سازی مسیرهای خروجی بر اساس شرط (برای دروازه‌های انحصاری و فراگیر)
    /// </summary>
    private async Task ActivateConditionalOutgoingFlowsAsync(
        BpmnProcessState state,
        ElementCompleted @event,
        GatewayInfo gatewayInfo,
        bool activateAllValidPaths,  // برای دروازه فراگیر true و برای دروازه انحصاری false
        CancellationToken cancellationToken)
    {
        // دریافت جریان‌های خروجی از المان فعلی
        var outgoingFlows = await GetOutgoingFlowsAsync(state, @event.ElementId, cancellationToken);
        
        if (outgoingFlows == null || !outgoingFlows.Any())
        {
            _logger.LogDebug("No outgoing flows found for gateway {GatewayId} in process {ProcessInstanceId}",
                @event.ElementId, @event.ProcessInstanceId);
            return;
        }
        
        _logger.LogDebug("Evaluating conditions for {FlowCount} outgoing flows from gateway {GatewayId} in process {ProcessInstanceId}",
            outgoingFlows.Count, @event.ElementId, @event.ProcessInstanceId);
            
        // یافتن مسیر پیش‌فرض (اگر موجود باشد)
        var defaultFlow = outgoingFlows.FirstOrDefault(f => f.IsDefault);
        
        // لیست مسیرهایی که شرط آنها برقرار است
        var validFlows = new List<FlowInfo>();
        
        // بررسی شرط هر مسیر
        foreach (var flow in outgoingFlows.Where(f => !f.IsDefault))
        {
            if (string.IsNullOrEmpty(flow.Condition))
            {
                // بدون شرط، معتبر است
                _logger.LogDebug("Flow {FlowId} has no condition, treating as valid", flow.Id);
                validFlows.Add(flow);
            }
            else
            {
                // ارزیابی شرط
                var isValid = await EvaluateConditionAsync(state, flow.Condition, cancellationToken);
                _logger.LogDebug("Flow {FlowId} condition evaluation result: {IsValid}", flow.Id, isValid);
                
                if (isValid)
                {
                    validFlows.Add(flow);
                    
                    // اگر دروازه انحصاری است، فقط اولین مسیر معتبر را می‌پذیریم
                    if (!activateAllValidPaths)
                    {
                        _logger.LogDebug("Exclusive gateway taking only first valid path: {FlowId}", flow.Id);
                        break;
                    }
                }
            }
        }
        
        // اگر هیچ مسیر معتبری یافت نشد و مسیر پیش‌فرض داریم، از آن استفاده می‌کنیم
        if (!validFlows.Any() && defaultFlow != null)
        {
            _logger.LogDebug("No valid paths found, taking default flow: {FlowId}", defaultFlow.Id);
            validFlows.Add(defaultFlow);
        }
        
        if (!validFlows.Any())
        {
            _logger.LogWarning("No valid paths found and no default flow for gateway {GatewayId} in process {ProcessInstanceId}",
                @event.ElementId, @event.ProcessInstanceId);
        }
        
        // فعال‌سازی مسیرهای معتبر
        foreach (var flow in validFlows)
        {
            await ActivateElementAsync(state, flow.TargetElementId, flow.TargetElementType, 
                @event.ElementId, flow.Id, cancellationToken);
        }
    }
    
    /// <summary>
    /// فعال‌سازی یک المان
    /// </summary>
    private async Task ActivateElementAsync(
        BpmnProcessState state,
        string elementId,
        string elementType,
        string sourceElementId,
        string sequenceFlowId,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Activating element {ElementId} of type {ElementType} via flow {FlowId} in process {ProcessInstanceId}",
            elementId, elementType, sequenceFlowId, state.ProcessInstanceId);
            
        // انتشار رویداد فعال‌سازی المان
        await _eventBus.PublishAsync(new ElementActivating
        {
            ProcessInstanceId = state.ProcessInstanceId,
            ElementId = elementId,
            ElementType = elementType,
            SourceElementId = sourceElementId,
            SequenceFlowId = sequenceFlowId
        }, cancellationToken);
        
        // کمی صبر برای پردازش رویداد
        await Task.Delay(50, cancellationToken);
        
        // انتشار رویداد فعال شدن المان
        await _eventBus.PublishAsync(new ElementActivated
        {
            ProcessInstanceId = state.ProcessInstanceId,
            ElementId = elementId,
            ElementType = elementType
        }, cancellationToken);
    }
    
    /// <summary>
    /// بررسی امکان ادغام دروازه موازی
    /// </summary>
    private async Task<bool> CanMergeParallelGatewayAsync(
        BpmnProcessState state,
        ElementCompleted @event,
        GatewayInfo gatewayInfo,
        CancellationToken cancellationToken)
    {
        // برای دروازه موازی، باید توکن از همه مسیرهای ورودی دریافت شده باشد
        
        // شمارش تعداد توکن‌هایی که به این دروازه رسیده‌اند
        // در یک پیاده‌سازی واقعی، این اطلاعات باید از یک مخزن نشانه‌ها استخراج شود
        
        // بررسی تعداد توکن‌های فعال برای این گیت‌وی در وضعیت فرآیند
        var receivedTokensCount = state.ActiveElements.Count(e => e == @event.ElementId);
            
        // اضافه کردن توکن فعلی اگر هنوز شمارش نشده است
        if (!state.ActiveElements.Contains(@event.ElementId))
        {
            receivedTokensCount++;
        }
        
        _logger.LogDebug("Parallel gateway {GatewayId} has received {ReceivedCount} tokens out of {TotalCount} required",
            @event.ElementId, receivedTokensCount, gatewayInfo.IncomingFlows.Count());
            
        // برای ادغام کامل، باید تعداد توکن‌ها برابر با تعداد مسیرهای ورودی باشد
        return receivedTokensCount >= gatewayInfo.IncomingFlows.Count();
    }
    
    /// <summary>
    /// بررسی امکان ادغام دروازه فراگیر
    /// </summary>
    private async Task<bool> CanMergeInclusiveGatewayAsync(
        BpmnProcessState state,
        ElementCompleted @event,
        GatewayInfo gatewayInfo,
        CancellationToken cancellationToken)
    {
        // برای دروازه فراگیر، باید توکن از همه مسیرهای ورودی فعال دریافت شده باشد
        
        // شناسایی مسیرهای ورودی فعال
        // در یک پیاده‌سازی واقعی، باید مسیرهای فعال را بر اساس تجزیه و تحلیل جریان تشخیص داد
        // برای این نمونه، فرض می‌کنیم همه مسیرهای ورودی فعال هستند
        
        // دریافت تعریف BPMN
        var definitions = _definitionStorage.GetParsedDefinition(state.DeploymentKey);
        if (definitions == null)
        {
            _logger.LogWarning("BPMN definition not found for deployment key {DeploymentKey}", state.DeploymentKey);
            return false;
        }
        
        // یافتن فرآیند
        var process = FindProcess(definitions, state.ProcessDefinitionId);
        if (process == null)
        {
            _logger.LogWarning("Process definition not found with ID {ProcessId} in deployment {DeploymentKey}", 
                state.ProcessDefinitionId, state.DeploymentKey);
            return false;
        }
        
        // تعیین مسیرهای ورودی فعال
        var activeIncomingFlows = DetermineActiveIncomingFlows(process, state, @event.ElementId);
        
        // شمارش تعداد توکن‌هایی که به این دروازه رسیده‌اند
        var receivedTokensCount = state.ActiveElements.Count(e => e == @event.ElementId);
            
        // اضافه کردن توکن فعلی اگر هنوز شمارش نشده است
        if (!state.ActiveElements.Contains(@event.ElementId))
        {
            receivedTokensCount++;
        }
        
        _logger.LogDebug("Inclusive gateway {GatewayId} has received {ReceivedCount} tokens out of {ActiveCount} active paths",
            @event.ElementId, receivedTokensCount, activeIncomingFlows.Count);
            
        // برای ادغام، باید تعداد توکن‌ها برابر با تعداد مسیرهای ورودی فعال باشد
        return receivedTokensCount >= activeIncomingFlows.Count;
    }
    
    /// <summary>
    /// تعیین مسیرهای ورودی فعال برای یک دروازه فراگیر
    /// </summary>
    private List<string> DetermineActiveIncomingFlows(
        BpmnProcess process,
        BpmnProcessState state,
        string gatewayId)
    {
        // یافتن جریان‌های ورودی
        var incomingFlows = FindIncomingFlows(process, gatewayId);
        
        // در یک پیاده‌سازی واقعی، باید تعیین کرد کدام مسیرها فعال هستند
        // با بررسی توکن‌های فعال، تاریخچه اجرا، و شرایط مسیرها
        
        // برای سادگی در این نمونه، فرض می‌کنیم همه مسیرهای ورودی فعال هستند
        return incomingFlows.Select(f => f.id).ToList();
    }
    
    /// <summary>
    /// دریافت اطلاعات یک دروازه
    /// </summary>
    private async Task<GatewayInfo> GetGatewayInfoAsync(
        BpmnProcessState state,
        string gatewayId,
        CancellationToken cancellationToken)
    {
        // دریافت تعریف BPMN از حافظه
        var definitions = _definitionStorage.GetParsedDefinition(state.DeploymentKey);
        if (definitions == null)
        {
            _logger.LogWarning("BPMN definition not found for deployment key {DeploymentKey}", state.DeploymentKey);
            return new GatewayInfo 
            { 
                Id = gatewayId,
                IsJoin = false,
                IncomingFlows = new List<string>(),
                OutgoingFlows = new List<string>()
            };
        }

        // یافتن فرآیند
        var process = FindProcess(definitions, state.ProcessDefinitionId);
        if (process == null)
        {
            _logger.LogWarning("Process definition not found with ID {ProcessId} in deployment {DeploymentKey}", 
                state.ProcessDefinitionId, state.DeploymentKey);
            return new GatewayInfo 
            { 
                Id = gatewayId,
                IsJoin = false,
                IncomingFlows = new List<string>(),
                OutgoingFlows = new List<string>()
            };
        }

        // یافتن المان گیت‌وی در فرآیند
        var gateway = FindElementById(process, gatewayId) as BpmnGateway;
        if (gateway == null)
        {
            _logger.LogWarning("Gateway element not found with ID {GatewayId} in process {ProcessId}", 
                gatewayId, state.ProcessDefinitionId);
            return new GatewayInfo 
            { 
                Id = gatewayId,
                IsJoin = false,
                IncomingFlows = new List<string>(),
                OutgoingFlows = new List<string>()
            };
        }

        // یافتن جریان‌های ورودی و خروجی
        var incomingFlows = FindIncomingFlows(process, gatewayId);
        var outgoingFlows = FindOutgoingFlows(process, gatewayId);

        // تشخیص اینکه آیا گیت‌وی Join است یا Split
        // اگر تعداد جریان‌های ورودی بیش از یکی باشد، Join است
        bool isJoin = incomingFlows.Count > 1;

        return new GatewayInfo
        {
            Id = gatewayId,
            IsJoin = isJoin,
            IncomingFlows = incomingFlows.Select(f => f.id).ToList(),
            OutgoingFlows = outgoingFlows.Select(f => f.id).ToList()
        };
    }
    
    /// <summary>
    /// دریافت جریان‌های خروجی از یک المان
    /// </summary>
    private async Task<List<FlowInfo>> GetOutgoingFlowsAsync(
        BpmnProcessState state,
        string elementId,
        CancellationToken cancellationToken)
    {
        // دریافت تعریف BPMN از حافظه
        var definitions = _definitionStorage.GetParsedDefinition(state.DeploymentKey);
        if (definitions == null)
        {
            _logger.LogWarning("BPMN definition not found for deployment key {DeploymentKey}", state.DeploymentKey);
            return new List<FlowInfo>();
        }

        // یافتن فرآیند
        var process = FindProcess(definitions, state.ProcessDefinitionId);
        if (process == null)
        {
            _logger.LogWarning("Process definition not found with ID {ProcessId} in deployment {DeploymentKey}", 
                state.ProcessDefinitionId, state.DeploymentKey);
            return new List<FlowInfo>();
        }

        // یافتن جریان‌های خروجی
        var outgoingFlows = FindOutgoingFlows(process, elementId);
        if (!outgoingFlows.Any())
        {
            _logger.LogDebug("No outgoing flows found for element {ElementId} in process {ProcessId}", 
                elementId, state.ProcessDefinitionId);
            return new List<FlowInfo>();
        }

        var result = new List<FlowInfo>();

        // تبدیل به اطلاعات جریان
        foreach (var flow in outgoingFlows)
        {
            // یافتن المان هدف
            var targetElement = FindElementById(process, flow.targetRef);
            if (targetElement == null)
            {
                _logger.LogWarning("Target element not found with ID {TargetId} for flow {FlowId}", 
                    flow.targetRef, flow.id);
                continue;
            }

            // تشخیص نوع المان هدف
            var targetElementType = GetElementType(targetElement);

            // آیا این جریان، جریان پیش‌فرض است؟
            bool isDefault = false;
            
            // بررسی گیت‌وی منبع
            var sourceElement = FindElementById(process, flow.sourceRef);
            if (sourceElement is BpmnGateway gateway)
            {
                if (gateway is BpmnExclusiveGateway exclusiveGateway)
                {
                    isDefault = exclusiveGateway.@default == flow.id;
                }
                else if (gateway is BpmnInclusiveGateway inclusiveGateway)
                {
                    isDefault = inclusiveGateway.@default == flow.id;
                }
            }

            result.Add(new FlowInfo
            {
                Id = flow.id,
                SourceElementId = flow.sourceRef,
                TargetElementId = flow.targetRef,
                TargetElementType = targetElementType,
                Condition = flow.conditionExpression?.Text.ToString(), // تبدیل شرط به رشته
                IsDefault = isDefault
            });
        }

        return result;
    }

    /// <summary>
    /// یافتن فرآیند با شناسه مشخص در تعریف BPMN
    /// </summary>
    private BpmnProcess FindProcess(BpmnDefinitions definitions, string processId)
    {
        if (definitions?.Items == null || !definitions.Items.Any())
            return null;
            
        // جستجوی فرآیند
        var processes = definitions.Items
            .OfType<BpmnProcess>()
            .ToList();
            
        if (!processes.Any())
            return null;
            
        if (string.IsNullOrEmpty(processId))
            return processes.First(); // اگر شناسه مشخص نشده، اولین فرآیند را برمی‌گرداند
            
        return processes.FirstOrDefault(p => p.id == processId);
    }

    /// <summary>
    /// یافتن المان با شناسه مشخص در فرآیند
    /// </summary>
    private BpmnBaseElement FindElementById(BpmnProcess process, string elementId)
    {
        if (process?.Items == null || !process.Items.Any() || string.IsNullOrEmpty(elementId))
            return null;

        // بررسی همه المان‌های فرآیند
        foreach (var item in process.Items)
        {
            if (item is BpmnBaseElement element && element.id == elementId)
                return element;
                
            // بررسی المان‌های تو در تو (مانند SubProcess)
            if (item is BpmnSubProcess subProcess && subProcess.Items != null)
            {
                var subElement = FindElementInSubProcess(subProcess, elementId);
                if (subElement != null)
                    return subElement;
            }
        }
        
        // بررسی جریان‌های توالی
        var flows = process.Items.OfType<BpmnSequenceFlow>().ToList();
        return flows.FirstOrDefault(f => f.id == elementId);
    }

    /// <summary>
    /// جستجوی المان در زیرفرآیند
    /// </summary>
    private BpmnBaseElement FindElementInSubProcess(BpmnSubProcess subProcess, string elementId)
    {
        if (subProcess?.Items == null || !subProcess.Items.Any())
            return null;
            
        foreach (var item in subProcess.Items)
        {
            // فقط بررسی می‌کنیم که آیا این آیتم یک المان BPMN پایه با ID مورد نظر است
            if (item is BpmnBaseElement element && element.id == elementId)
                return element;
        }
        
        // پیاده‌سازی بازگشتی برای زیرفرآیندها را حذف می‌کنیم تا از خطای اجرا جلوگیری شود
        // در یک پیاده‌سازی کامل، باید روی همه المان‌های SubProcess بازگشتی فراخوانی شود
        
        return null;
    }

    /// <summary>
    /// یافتن جریان‌های ورودی به یک المان
    /// </summary>
    private List<BpmnSequenceFlow> FindIncomingFlows(BpmnProcess process, string elementId)
    {
        if (process?.Items == null || !process.Items.Any() || string.IsNullOrEmpty(elementId))
            return new List<BpmnSequenceFlow>();
            
        // یافتن جریان‌های توالی
        var flows = process.Items.OfType<BpmnSequenceFlow>().ToList();
        
        // فیلتر کردن جریان‌هایی که به المان مورد نظر وارد می‌شوند
        return flows.Where(f => f.targetRef == elementId).ToList();
    }

    /// <summary>
    /// یافتن جریان‌های خروجی از یک المان
    /// </summary>
    private List<BpmnSequenceFlow> FindOutgoingFlows(BpmnProcess process, string elementId)
    {
        if (process?.Items == null || !process.Items.Any() || string.IsNullOrEmpty(elementId))
            return new List<BpmnSequenceFlow>();
            
        // یافتن جریان‌های توالی
        var flows = process.Items.OfType<BpmnSequenceFlow>().ToList();
        
        // فیلتر کردن جریان‌هایی که از المان مورد نظر خارج می‌شوند
        return flows.Where(f => f.sourceRef == elementId).ToList();
    }

    /// <summary>
    /// تشخیص نوع المان
    /// </summary>
    private string GetElementType(BpmnBaseElement element)
    {
        if (element == null)
            return "unknown";
            
        // تشخیص نوع المان بر اساس کلاس آن
       
        if (element is BpmnUserTask)
            return "bpmn:UserTask";
        if (element is BpmnServiceTask)
            return "bpmn:ServiceTask";
        if (element is BpmnScriptTask)
            return "bpmn:ScriptTask";
        if (element is BpmnBusinessRuleTask)
            return "bpmn:BusinessRuleTask";
        if (element is BpmnManualTask)
            return "bpmn:ManualTask";
        if (element is BpmnReceiveTask)
            return "bpmn:ReceiveTask";
        if (element is BpmnSendTask)
            return "bpmn:SendTask";
        if (element is BpmnSubProcess)
            return "bpmn:SubProcess";
        if (element is BpmnCallActivity)
            return "bpmn:CallActivity";
        if (element is BpmnStartEvent)
            return "bpmn:StartEvent";
        if (element is BpmnEndEvent)
            return "bpmn:EndEvent";
        if (element is BpmnIntermediateCatchEvent)
            return "bpmn:IntermediateCatchEvent";
        if (element is BpmnIntermediateThrowEvent)
            return "bpmn:IntermediateThrowEvent";
        if (element is BpmnBoundaryEvent)
            return "bpmn:BoundaryEvent";
        if (element is BpmnExclusiveGateway)
            return "bpmn:ExclusiveGateway";
        if (element is BpmnParallelGateway)
            return "bpmn:ParallelGateway";
        if (element is BpmnInclusiveGateway)
            return "bpmn:InclusiveGateway";
        if (element is BpmnComplexGateway)
            return "bpmn:ComplexGateway";
        if (element is BpmnEventBasedGateway)
            return "bpmn:EventBasedGateway";
        if (element is BpmnTask)
            return "bpmn:Task";     
        // برای المان‌های ناشناخته
        return element.GetType().Name;
    }
    
    /// <summary>
    /// ارزیابی یک شرط
    /// </summary>
    private async Task<bool> EvaluateConditionAsync(
        BpmnProcessState state,
        string condition,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(condition))
            return true; // اگر شرط خالی باشد، همیشه صحیح است
            
        try
        {
            // در پیاده‌سازی واقعی، باید از یک موتور اسکریپت برای ارزیابی شرط استفاده شود
            // و متغیرهای فرآیند را در ارزیابی در نظر گرفت
            
            // مثال ساده برای ارزیابی شرط‌های ساده
            if (condition.Contains("=="))
            {
                var parts = condition.Split(new[] { "==" }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    var leftPart = parts[0].Trim();
                    var rightPart = parts[1].Trim();
                    
                    // اگر متغیر در شرط وجود داشته باشد، جایگزین می‌کنیم
                    if (leftPart.StartsWith("${") && leftPart.EndsWith("}"))
                    {
                        var varName = leftPart.Substring(2, leftPart.Length - 3);
                        if (state.Variables.TryGetValue(varName, out var value))
                        {
                            return value?.ToString() == rightPart.Trim('"', '\'');
                        }
                    }
                }
            }
            
            // پیاده‌سازی پیشرفته‌تر ارزیابی شرط در اینجا
            
            // برای سادگی در این نمونه
            _logger.LogWarning("Condition evaluation not fully implemented: {Condition}", condition);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating condition: {Condition}", condition);
            return false;
        }
    }
    
    /// <summary>
    /// کلاس اطلاعات دروازه
    /// </summary>
    private class GatewayInfo
    {
        public string Id { get; set; }
        public bool IsJoin { get; set; }
        public IEnumerable<string> IncomingFlows { get; set; }
        public IEnumerable<string> OutgoingFlows { get; set; }
    }
    
    /// <summary>
    /// کلاس اطلاعات جریان
    /// </summary>
    private class FlowInfo
    {
        public string Id { get; set; }
        public string SourceElementId { get; set; }
        public string TargetElementId { get; set; }
        public string TargetElementType { get; set; }
        public string Condition { get; set; }
        public bool IsDefault { get; set; }
    }

    /// <summary>
    /// پردازش فعال شدن رویداد مرزی
    /// </summary>
    private async Task HandleBoundaryEventTriggerAsync(
        BpmnProcessState state, 
        ElementCompleted @event,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Processing trigger of boundary event {EventId} in process {ProcessInstanceId}",
            @event.ElementId, @event.ProcessInstanceId);
        
        // دریافت تعریف BPMN برای تشخیص نوع رویداد مرزی (interrupting یا non-interrupting)
        var definition = _definitionStorage.GetParsedDefinition(state.DeploymentKey);
        if (definition == null)
        {
            _logger.LogWarning("BPMN definition not found for deployment key {DeploymentKey}", state.DeploymentKey);
            // فرض می‌کنیم که رویداد از نوع غیرقطع‌کننده است و فقط مسیر بعدی را فعال می‌کنیم
            await ActivateNextElementsAsync(state, @event, cancellationToken);
            return;
        }

        // یافتن رویداد مرزی در تعریف
        var boundaryEvent = FindBoundaryEvent(definition, state.ProcessDefinitionId, @event.ElementId);
        if (boundaryEvent == null)
        {
            _logger.LogWarning("Boundary event {ElementId} not found in definition", @event.ElementId);
            // فرض می‌کنیم که رویداد از نوع غیرقطع‌کننده است و فقط مسیر بعدی را فعال می‌کنیم
            await ActivateNextElementsAsync(state, @event, cancellationToken);
            return;
        }

        // تعیین فعالیتی که رویداد مرزی به آن متصل است
        string attachedToElementId = boundaryEvent.attachedToRef?.ToString();
        if (string.IsNullOrEmpty(attachedToElementId))
        {
            _logger.LogWarning("Boundary event {ElementId} has no valid attachedToRef", @event.ElementId);
            await ActivateNextElementsAsync(state, @event, cancellationToken);
            return;
        }

        // تعیین نوع رویداد مرزی (قطع‌کننده یا غیرقطع‌کننده)
        bool isInterrupting = boundaryEvent.cancelActivity;
        
        // اگر رویداد مرزی از نوع قطع‌کننده (interrupting) است
        if (isInterrupting)
        {
            _logger.LogDebug("Boundary event {ElementId} is interrupting. Canceling activity {ActivityId}",
                @event.ElementId, attachedToElementId);
            
            // انتشار رویداد لغو فعالیت متصل
            await _eventBus.PublishAsync(new Events.ActivityCancelledEvent
            {
                EventId = Guid.NewGuid(),
                ProcessInstanceId = @event.ProcessInstanceId,
                ElementId = attachedToElementId,
                Reason = $"Interrupted by boundary event {boundaryEvent.id}",
                Intent = "CANCELLED",
                Timestamp = DateTime.UtcNow
            }, cancellationToken);
            
            // کمی صبر برای پردازش رویداد
            await Task.Delay(50, cancellationToken);
        }
        else
        {
            _logger.LogDebug("Boundary event {ElementId} is non-interrupting. Activity {ActivityId} continues",
                @event.ElementId, attachedToElementId);
        }
        
        // در هر صورت، فعال‌سازی مسیر‌های خروجی از رویداد مرزی
        await ActivateNextElementsAsync(state, @event, cancellationToken);
    }

    /// <summary>
    /// یافتن رویداد مرزی با شناسه مشخص در تعریف BPMN
    /// </summary>
    private BpmnBoundaryEvent FindBoundaryEvent(BpmnDefinitions definitions, string processId, string eventId)
    {
        // یافتن فرآیند با شناسه مشخص
        var process = FindProcess(definitions, processId);
        if (process == null)
            return null;
        
        // جستجوی رویداد مرزی در فرآیند
        foreach (var item in process.Items)
        {
            if (item is BpmnBoundaryEvent boundaryEvent && boundaryEvent.id == eventId)
                return boundaryEvent;
        }
        
        return null;
    }
}