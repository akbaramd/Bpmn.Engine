using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Novin.Bpmn;
using Novin.Bpmn.Core;
using Novin.Bpmn.Models;
using Novin.Bpmn.V3;

public class BpmnV3ProcessInstance
{
    // اطلاعات پایه
    public string ProcessElementId { get; private set; }
    public string DefinitionXml { get; private set; }

    // توکن‌ها و رویدادها
    public ConcurrentDictionary<Guid, List<BaseEvent>> TokenEvents { get; private set; } = new();
    public ConcurrentBag<BpmnV3Token> Tokens { get; private set; } = new();
    
    // متغیرهای فرآیند
    public dynamic Variables = new ExpandoObject();
    
    // جدید: ساختارهای داده برای ردگیری نودها و فلوها
    public ConcurrentDictionary<string, NodeExecutionInfo> ExecutedNodes { get; private set; } = new();
    public ConcurrentDictionary<string, FlowExecutionInfo> ExecutedFlows { get; private set; } = new();
    
    [JsonIgnore]
    public BpmnDefinitions Definition => BpmnDefinitionSerializer.Deserialize(DefinitionXml);

    [JsonIgnore]
    public BpmnDefinitionsHandler DefinitionsHandler => new(Definition);

    public BpmnV3ProcessInstance(string processElementId, string definitionXml)
    {
        ProcessElementId = processElementId;
        DefinitionXml = definitionXml;
    }

    public BpmnV3Token CreateUnExecutableToken(string startElementId, string? flowElementId = null)
    {
        var token = new BpmnV3Token(startElementId, flowElementId);
        token.UnExecutable();
        Tokens.Add(token);
        
        // جدید: ثبت ایجاد توکن در نود
        TrackNodeExecution(startElementId, token.Id, false);
        
        if (!string.IsNullOrEmpty(flowElementId))
        {
            TrackFlowExecution(flowElementId, Guid.Empty, token.Id, false);
        }
        
        return token;
    }

    public BpmnV3Token CreateToken(string startElementId, string? flowElementId = null)
    {
        var token = new BpmnV3Token(startElementId, flowElementId);
        Tokens.Add(token);
        
        // جدید: ثبت ایجاد توکن در نود
        TrackNodeExecution(startElementId, token.Id, true);
        
        if (!string.IsNullOrEmpty(flowElementId))
        {
            TrackFlowExecution(flowElementId, Guid.Empty, token.Id, true);
        }
        
        return token;
    }

    public void AddEventToToken(Guid tokenId, BaseEvent bpmnEvent)
    {
        if (!TokenEvents.ContainsKey(tokenId))
        {
            TokenEvents[tokenId] = new List<BaseEvent>();
        }
        TokenEvents[tokenId].Add(bpmnEvent);
        
        // Initialize the event
        bpmnEvent.Initialize();
    }
    
    public async Task TriggerEventsForToken(Guid tokenId)
    {
        if (TokenEvents.TryGetValue(tokenId, out var events))
        {
            foreach (var bpmnEvent in events)
            {
                await bpmnEvent.Trigger();
            }
        }
        else
        {
            Console.WriteLine($"No events found for Token {tokenId}");
        }
    }
    
    // Moves a token to the next element based on routing logic
    public async Task MoveToken(BpmnV3Token token, bool? isExecutable = null)
    {
        if (token.Status != TokenStatus.Active)
        {
            Console.WriteLine($"Token {token.Id} is not active and cannot be moved.");
            return;
        }

        var currentElement = DefinitionsHandler.GetElementById(token.CurrentElementId);
        
        // جدید: ثبت تغییر در وضعیت توکن
        bool isExecutableValue = isExecutable ?? token.IsExecutable;
        
        // بررسی Boundary Event‌های متصل به نود
        // ادامه مدیریت توکن
        if (currentElement is BpmnGateway gateway)
        {
            await HandleGateway(token, gateway, isExecutable);
        }
        else
        {
            HandleNormalFlow(token, currentElement, isExecutable);
        }

        if (TokenEvents.TryGetValue(token.Id, out var list))
        {
            list.Clear();
        }
    }

    public async Task TriggerSpecificEvent<T>(Guid nodeId) where T : BaseEvent
    {
        if (TokenEvents.TryGetValue(nodeId, out var events))
        {
            foreach (var bpmnEvent in events.OfType<T>())
            {
                await bpmnEvent.Trigger();
            }
        }
        else
        {
            Console.WriteLine($"No events of type {typeof(T).Name} found for Node {nodeId}");
        }
    }
    
    private async Task HandleGateway(BpmnV3Token token, BpmnGateway gateway, bool? isExecutable)
    {
        switch (gateway)
        {
            case BpmnExclusiveGateway:
                HandleExclusiveGateway(token, gateway, isExecutable);
                break;
            case BpmnParallelGateway:
                HandleParallelGateway(token, gateway, isExecutable);
                break;
            case BpmnInclusiveGateway:
                await HandleInclusiveGateway(token, gateway, isExecutable);
                break;
            case BpmnComplexGateway complexGateway:
                await HandleComplexGateway(token, complexGateway, isExecutable);
                break;
            case BpmnEventBasedGateway eventBasedGateway:
                var handler = new EventBasedGatewayHandler(this);
                handler.HandleEventBasedGateway(token, eventBasedGateway, isExecutable);
                break;
            default:
                Console.WriteLine($"Unsupported gateway type: {gateway.GetType().Name}");
                break;
        }
    }

    private void HandleExclusiveGateway(BpmnV3Token token, BpmnGateway gateway, bool? isExecutable)
    {
        // بررسی وضعیت اجرایی توکن فعلی
        bool tokenIsExecutable = isExecutable ?? token.IsExecutable;
        
        var outgoingFlows = DefinitionsHandler.GetOutgoingSequenceFlows(gateway);
        
        // اگر توکن غیرفعال است، تمام مسیرهای خروجی را به صورت غیرفعال رهگیری کن
        if (!tokenIsExecutable)
        {
            foreach (var flow in outgoingFlows)
            {
                TrackFlowExecution(flow.id, token.Id, Guid.Empty, false);
                var inactiveToken = CreateUnExecutableToken(flow.targetRef, flow.id);
                inactiveToken.ParentTokenId = token.Id;
            }
            token.Complete();
            return;
        }
        
        // یافتن مسیر پیش‌فرض (در صورت وجود)
        BpmnSequenceFlow defaultFlow = null;
        if (gateway is BpmnExclusiveGateway exclusiveGateway && !string.IsNullOrEmpty(exclusiveGateway.@default))
        {
            defaultFlow = outgoingFlows.FirstOrDefault(f => f.id == exclusiveGateway.@default);
        }
        
        // ارزیابی شرط‌ها فقط اگر توکن فعال باشد
        var selectedFlow = outgoingFlows
            .Where(flow => defaultFlow == null || flow.id != defaultFlow.id) // مسیر پیش‌فرض را در ارزیابی شرط‌ها نادیده می‌گیریم
            .FirstOrDefault(flow =>
                DefinitionsHandler.EvaluateCondition(flow, token, this).GetAwaiter().GetResult());

        // اگر هیچ شرطی برقرار نشد و مسیر پیش‌فرض وجود دارد، از آن استفاده می‌کنیم
        if (selectedFlow == null && defaultFlow != null)
        {
            selectedFlow = defaultFlow;
            Console.WriteLine($"Using default flow {defaultFlow.id} as no conditions were met");
        }

        if (selectedFlow != null)
        {
            // ثبت استفاده از فلو
            TrackFlowExecution(selectedFlow.id, token.Id, Guid.Empty, true);
            
            token.MoveTo(selectedFlow.targetRef, selectedFlow.id);
            
            // ثبت نود جدید
            TrackNodeExecution(selectedFlow.targetRef, token.Id, true);
            
            // برای مسیرهای غیرانتخابی، ایجاد توکن غیراجرایی برای نمایش
            foreach (var flow in outgoingFlows.Where(f => f.id != selectedFlow.id))
            {
                // ثبت جریان و نود به صورت غیرفعال برای نمایش
                TrackFlowExecution(flow.id, token.Id, Guid.Empty, false);
                CreateUnExecutableToken(flow.targetRef, flow.id);
            }
        }
        else
        {
            // هیچ مسیری (حتی پیش‌فرض) نبود
            Console.WriteLine($"No valid outgoing flow found for exclusive gateway {gateway.id}. Token will expire.");
            token.Expire();
        }
    }
    
    private async Task<bool> HandleBoundaryEvent(BpmnBoundaryEvent boundaryEvent, BpmnV3Token token)
    {
        foreach (var eventDefinition in boundaryEvent.Items)
        {
            if (eventDefinition is BpmnErrorEventDefinition errorEventDefinition)
            {
                Console.WriteLine($"Handling error event on boundary of element {boundaryEvent.attachedToRef.Name}");

                // انتقال توکن به مسیر مرتبط با Error Event
                var outgoingFlows = DefinitionsHandler.GetOutgoingSequenceFlows(boundaryEvent);
                if (outgoingFlows.Any())
                {
                    var flow = outgoingFlows.First(); // مسیر جدید
                    
                    // ثبت استفاده از فلو و نود جدید
                    TrackFlowExecution(flow.id, token.Id, Guid.Empty, token.IsExecutable);
                    
                    token.MoveTo(flow.targetRef, flow.id);
                    
                    // ثبت نود جدید
                    TrackNodeExecution(flow.targetRef, token.Id, token.IsExecutable);
                    
                    return true; // مدیریت Event کامل شد
                }
                else
                {
                    Console.WriteLine($"Error event on {boundaryEvent.id} has no outgoing flows.");
                }
            }
            else if (eventDefinition is BpmnTimerEventDefinition timerEventDefinition)
            {
                // پیاده‌سازی مناسب برای Timer Event
                Console.WriteLine($"Handling timer event {boundaryEvent.id}...");
                
                // به‌جای لاگ ساده، باید منطق تایمر پیاده‌سازی شود
                // ایجاد یک رویداد تایمر که پس از مدت مشخص شده فعال می‌شود
                // این کد تکمیل شده می‌تواند به نسخه‌های بعدی اضافه شود
                
                // برای نمایش می‌توانیم مسیر را به صورت غیرفعال نشان دهیم
                var outgoingFlows = DefinitionsHandler.GetOutgoingSequenceFlows(boundaryEvent);
                if (outgoingFlows.Any())
                {
                    foreach (var flow in outgoingFlows)
                    {
                        // ثبت مسیر به صورت غیرفعال برای نمایش در دیاگرام
                        TrackFlowExecution(flow.id, Guid.Empty, Guid.Empty, false);
                        
                        // ثبت نود بعدی به صورت غیرفعال
                        TrackNodeExecution(flow.targetRef, Guid.Empty, false);
                    }
                }
                
                // نیاز به پیاده‌سازی واقعی در آینده
                return true;
            }
            else if (eventDefinition is BpmnSignalEventDefinition signalEventDefinition)
            {
                // پیاده‌سازی مناسب برای Signal Event
                Console.WriteLine($"Handling signal event {boundaryEvent.id}...");
                
                // مشابه تایمر، باید منطق سیگنال پیاده‌سازی شود
                // سیگنال می‌تواند از بیرون فرآیند ارسال شود
                
                // برای نمایش می‌توانیم مسیر را به صورت غیرفعال نشان دهیم
                var outgoingFlows = DefinitionsHandler.GetOutgoingSequenceFlows(boundaryEvent);
                if (outgoingFlows.Any())
                {
                    foreach (var flow in outgoingFlows)
                    {
                        // ثبت مسیر به صورت غیرفعال برای نمایش در دیاگرام
                        TrackFlowExecution(flow.id, Guid.Empty, Guid.Empty, false);
                        
                        // ثبت نود بعدی به صورت غیرفعال
                        TrackNodeExecution(flow.targetRef, Guid.Empty, false);
                    }
                }
                
                // نیاز به پیاده‌سازی واقعی در آینده
                return true;
            }
        }

        return false; // هیچ Boundary Event‌ای اجرا نشد
    }

    private void HandleParallelGateway(BpmnV3Token token, BpmnGateway gateway, bool? isExecutable)
    {
        token.SetPendingToMerge();

        var incomingFlows = DefinitionsHandler.GetIncomingSequenceFlows(gateway);
        
        // If there's only one incoming flow, we don't need to wait for merge
        if (incomingFlows.Count <= 1)
        {
            // Process incoming token directly without waiting
            ProcessParallelGatewayOutgoingFlows(token, gateway, isExecutable);
            return;
        }
        
        var tokensAtGateway = Tokens
            .Where(t => t.CurrentElementId == gateway.id && t.Status == TokenStatus.PendingToMerge).ToList();

        if (tokensAtGateway.Count == incomingFlows.Count)
        {
            // بررسی آیا حداقل یکی از توکن‌های ورودی فعال است
            bool anyIncomingTokenExecutable = tokensAtGateway.Any(t => t.IsExecutable);
            
            foreach (var t in tokensAtGateway)
            {
                t.Complete();
            }

            var parentToken = token.ParentTokenId != null
                ? Tokens.FirstOrDefault(t => t.Id == token.ParentTokenId)
                : token;
                
            if (parentToken != null)
            {
                parentToken.Reactivate();
                
                // اگر نود ورودی فعال است، isExecutable باید true باشد
                // در غیر این صورت، از مقدار پارامتر ورودی یا توکن والد استفاده می‌شود
                bool parentIsExecutable = anyIncomingTokenExecutable || (isExecutable ?? parentToken.IsExecutable);

                ProcessParallelGatewayOutgoingFlows(parentToken, gateway, parentIsExecutable);
            }
        }
        else
        {
            Console.WriteLine($"Waiting for more tokens to merge at parallel gateway {gateway.id}");
        }
    }

    // Extract common processing logic to a separate method
    private void ProcessParallelGatewayOutgoingFlows(BpmnV3Token token, BpmnGateway gateway, bool? isExecutable)
    {
        // Check if token is executable
        bool tokenIsExecutable = isExecutable ?? token.IsExecutable;
        
        var outgoingFlows = DefinitionsHandler.GetOutgoingSequenceFlows(gateway);
        foreach (var flow in outgoingFlows)
        {
            // جدید: ثبت استفاده از فلو
            TrackFlowExecution(flow.id, token.Id, Guid.Empty, tokenIsExecutable);
            
            if (tokenIsExecutable)
            {
                var newToken = CreateToken(flow.targetRef, flow.id);
                newToken.ParentTokenId = token.Id;
            }
            else
            {
                var inactiveToken = CreateUnExecutableToken(flow.targetRef, flow.id);
                inactiveToken.ParentTokenId = token.Id;
            }
        }
    }

    private async Task HandleInclusiveGateway(BpmnV3Token token, BpmnGateway gateway, bool? isExecutable)
    {
        token.SetPendingToMerge();

        var incomingFlows = DefinitionsHandler.GetIncomingSequenceFlows(gateway);
        
        // If there's only one incoming flow, we don't need to wait for merge
        if (incomingFlows.Count <= 1)
        {
            // Process incoming token directly without waiting
            ProcessInclusiveGatewayOutgoingFlows(token, gateway, isExecutable);
            return;
        }
        
        // در استاندارد BPMN، گیت‌وی Inclusive باید منتظر تمام توکن‌های فعال باشد که ممکن است از مسیرهای ورودی برسند
        // ابتدا، شناسایی مسیرهای ورودی فعال بر اساس شرایط
        var activeIncomingFlows = new HashSet<string>();
        
        // یافتن توکن‌های منتظر در گیت‌وی
        var tokensAtGateway = Tokens
            .Where(t => t.CurrentElementId == gateway.id && t.Status == TokenStatus.PendingToMerge).ToList();
        
        // بررسی اینکه آیا تمام توکن‌های مورد انتظار رسیده‌اند
        bool allExpectedTokensArrived = true;
        
        // بررسی آیا حداقل یکی از توکن‌های ورودی فعال است
        bool anyIncomingTokenExecutable = tokensAtGateway.Any(t => t.IsExecutable);
        
        // مسیرهای ورودی که توکن‌های آنها به گیت‌وی رسیده‌اند
        var receivedFlows = tokensAtGateway
            .Where(t => t.History.Count >= 2)
            .Select(t => t.History[t.History.Count - 2].FlowId)
            .ToHashSet();
        
        // شناسایی مسیرهای ورودی که فعال هستند (شرط آنها برقرار است)
        foreach (var flow in incomingFlows)
        {
            if (receivedFlows.Contains(flow.id))
            {
                activeIncomingFlows.Add(flow.id);
            }
        }
        
        // تایید اینکه تمام مسیرهای فعال توکن دریافت کرده‌اند
        if (activeIncomingFlows.Count == tokensAtGateway.Count)
        {
            foreach (var t in tokensAtGateway)
            {
                t.Complete();
            }

            ProcessInclusiveGatewayOutgoingFlows(token, gateway, isExecutable);
        }
        else
        {
            Console.WriteLine($"Waiting for more tokens to merge at inclusive gateway {gateway.id}");
        }
    }

    // Extract common processing logic to a separate method
    private async Task ProcessInclusiveGatewayOutgoingFlows(BpmnV3Token token, BpmnGateway gateway, bool? isExecutable)
    {
        var parentToken = token.ParentTokenId != null
            ? Tokens.FirstOrDefault(t => t.Id == token.ParentTokenId)
            : token;

        if (parentToken != null)
        {
            // بررسی آیا حداقل یکی از توکن‌های ورودی فعال است
            bool isTokenExecutable = isExecutable ?? token.IsExecutable;
            
            // اطمینان از فعال بودن توکن والد اگر حداقل یک توکن ورودی فعال باشد
            if (isTokenExecutable && !parentToken.IsExecutable)
            {
                parentToken.Executable();
            }
            
            var outgoingFlows = DefinitionsHandler.GetOutgoingSequenceFlows(gateway);

            // بررسی مسیرهای خروجی و ایجاد توکن‌های جدید
            foreach (var flow in outgoingFlows)
            {
                // شرط‌ها فقط وقتی ارزیابی می‌شوند که حداقل یک توکن ورودی فعال باشد
                bool flowConditionMet = await DefinitionsHandler.EvaluateCondition(flow, parentToken, this);
                
                // فلو وقتی فعال است که هم شرط آن برقرار باشد و هم حداقل یک توکن ورودی فعال باشد
                bool flowExecutable = flowConditionMet && isTokenExecutable;
                
                // ثبت استفاده از فلو
                TrackFlowExecution(flow.id, parentToken.Id, Guid.Empty, flowExecutable);
                
                if (flowExecutable)
                {
                    var newToken = CreateToken(flow.targetRef, flow.id);
                    newToken.ParentTokenId = parentToken.Id;
                }
                else
                {
                    var inactiveToken = CreateUnExecutableToken(flow.targetRef, flow.id);
                    inactiveToken.ParentTokenId = parentToken.Id;
                }
            }
        }
    }

    // پیاده‌سازی گیت‌وی پیچیده (Complex Gateway)
    private async Task HandleComplexGateway(BpmnV3Token token, BpmnComplexGateway gateway, bool? isExecutable)
    {
        token.SetPendingToMerge();
        
        var incomingFlows = DefinitionsHandler.GetIncomingSequenceFlows(gateway);
        
        // If there's only one incoming flow, we don't need to wait for merge
        if (incomingFlows.Count <= 1)
        {
            // Process incoming token directly without waiting
            await ProcessComplexGatewayOutgoingFlows(token, gateway, isExecutable);
            return;
        }
        
        // توکن‌های رسیده به گیت‌وی را جمع‌آوری می‌کنیم
        var tokensAtGateway = Tokens
            .Where(t => t.CurrentElementId == gateway.id && t.Status == TokenStatus.PendingToMerge).ToList();
        
        // شرط ادغام را ارزیابی می‌کنیم - ابتدا بررسی می‌کنیم آیا شرط ادغام وجود دارد
        bool canActivate = false;
        if (gateway.activationCondition != null)
        {
            // پیاده‌سازی شرط ادغام با استفاده از موتور اسکریپت
            try
            {
                var scriptHandler = new ScriptHandler();
                var globals = new BpmnV3ScriptGlobals { Instance = this };
                var expression = string.Join(" ", gateway.activationCondition.Text);
                canActivate = await scriptHandler.EvaluateConditionAsync(expression, globals);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error evaluating complex gateway activation condition: {ex.Message}");
                canActivate = false;
            }
            Console.WriteLine($"Complex gateway {gateway.id} activation condition evaluated to {canActivate}");
        }
        else
        {
            // اگر شرط ادغام تعریف نشده باشد، به صورت پیش‌فرض همه توکن‌ها باید برسند
            canActivate = tokensAtGateway.Count >= incomingFlows.Count;
        }
        
        // بررسی آیا حداقل یکی از توکن‌های ورودی فعال است
        bool anyIncomingTokenExecutable = tokensAtGateway.Any(t => t.IsExecutable);
        
        // اگر شرط ادغام برقرار است، توکن‌ها را ادغام می‌کنیم
        if (canActivate)
        {
            // همه توکن‌ها را تکمیل می‌کنیم
            foreach (var t in tokensAtGateway)
            {
                t.Complete();
            }
            
            // پیدا کردن توکن والد
            var parentToken = token.ParentTokenId != null
                ? Tokens.FirstOrDefault(t => t.Id == token.ParentTokenId)
                : token;
                
            if (parentToken != null)
            {
                // اطمینان از فعال بودن توکن والد اگر حداقل یک توکن ورودی فعال باشد
                if (anyIncomingTokenExecutable && !parentToken.IsExecutable)
                {
                    parentToken.Executable();
                }
                
                parentToken.Reactivate();
                
                await ProcessComplexGatewayOutgoingFlows(parentToken, gateway, anyIncomingTokenExecutable);
            }
        }
        else
        {
            Console.WriteLine($"Waiting for more tokens or condition to be met at complex gateway {gateway.id}");
        }
    }

    // Extract common processing logic to a separate method
    private async Task ProcessComplexGatewayOutgoingFlows(BpmnV3Token token, BpmnComplexGateway gateway, bool? isExecutable)
    {
        bool tokenIsExecutable = isExecutable ?? token.IsExecutable;
        
        // مسیرهای خروجی را بررسی می‌کنیم
        var outgoingFlows = DefinitionsHandler.GetOutgoingSequenceFlows(gateway);
        
        // یافتن مسیر پیش‌فرض (در صورت وجود)
        BpmnSequenceFlow defaultFlow = null;
        if (!string.IsNullOrEmpty(gateway.@default))
        {
            defaultFlow = outgoingFlows.FirstOrDefault(f => f.id == gateway.@default);
        }
        
        // اگر مسیر پیش‌فرض وجود دارد و هیچ شرطی برقرار نیست، فقط مسیر پیش‌فرض را دنبال می‌کنیم
        bool anyConditionMet = false;
        
        // برای هر مسیر خروجی، شرط آن را ارزیابی می‌کنیم
        foreach (var flow in outgoingFlows.Where(f => defaultFlow == null || f.id != defaultFlow.id))
        {
            bool flowConditionMet = await DefinitionsHandler.EvaluateCondition(flow, token, this);
            
            if (flowConditionMet)
            {
                anyConditionMet = true;
                bool flowExecutable = tokenIsExecutable;
                
                // ثبت استفاده از فلو
                TrackFlowExecution(flow.id, token.Id, Guid.Empty, flowExecutable);
                
                if (flowExecutable)
                {
                    var newToken = CreateToken(flow.targetRef, flow.id);
                    newToken.ParentTokenId = token.Id;
                }
                else
                {
                    var inactiveToken = CreateUnExecutableToken(flow.targetRef, flow.id);
                    inactiveToken.ParentTokenId = token.Id;
                }
            }
            else
            {
                // مسیر غیرفعال
                TrackFlowExecution(flow.id, token.Id, Guid.Empty, false);
                var inactiveToken = CreateUnExecutableToken(flow.targetRef, flow.id);
                inactiveToken.ParentTokenId = token.Id;
            }
        }
        
        // اگر هیچ شرطی برقرار نیست و مسیر پیش‌فرض وجود دارد
        if (!anyConditionMet && defaultFlow != null)
        {
            bool flowExecutable = tokenIsExecutable;
            
            // ثبت استفاده از مسیر پیش‌فرض
            TrackFlowExecution(defaultFlow.id, token.Id, Guid.Empty, flowExecutable);
            
            if (flowExecutable)
            {
                var newToken = CreateToken(defaultFlow.targetRef, defaultFlow.id);
                newToken.ParentTokenId = token.Id;
            }
            else
            {
                var inactiveToken = CreateUnExecutableToken(defaultFlow.targetRef, defaultFlow.id);
                inactiveToken.ParentTokenId = token.Id;
            }
        }
    }

    public List<BpmnV3Token> GetWaitingTokens()
    {
        return Tokens.Where(t => t.Status == TokenStatus.Waiting).ToList();
    }

    private void HandleNormalFlow(BpmnV3Token token, BpmnFlowElement element, bool? isExecutable)
    {
        var outgoingFlows = DefinitionsHandler.GetOutgoingSequenceFlows(element);

        if (!outgoingFlows.Any())
        {
            Console.WriteLine($"No outgoing flows for element {element.id}.");
            token.Complete();
            return;
        }

        if (element is BpmnUserTask)
        {
            token.SetWaiting(); // Set the token to waiting for user task
        }
        else
        {
            // تنظیم وضعیت اجرایی توکن بر اساس پارامتر ورودی
            bool tokenIsExecutable = isExecutable ?? token.IsExecutable;
            token.SetExecutable(tokenIsExecutable);
            
            foreach (var flow in outgoingFlows)
            {
                // جدید: ثبت استفاده از فلو
                TrackFlowExecution(flow.id, token.Id, Guid.Empty, tokenIsExecutable);
                
                token.MoveTo(flow.targetRef, flow.id);
                
                // جدید: ثبت نود جدید
                TrackNodeExecution(flow.targetRef, token.Id, tokenIsExecutable);
            }
        }
    }
    
    // جدید: متدهای مربوط به ردیابی اجرای نودها و فلوها
    
    /// <summary>
    /// ثبت اطلاعات اجرای یک نود
    /// </summary>
    public void TrackNodeExecution(string nodeId, Guid tokenId, bool isExecutable)
    {
        ExecutedNodes.AddOrUpdate(
            nodeId,
            new NodeExecutionInfo
            {
                NodeId = nodeId,
                TokenIds = new List<Guid> { tokenId },
                ExecutionCount = 1,
                IsActive = isExecutable,
                LastExecutionTime = DateTime.UtcNow
            },
            (key, existingInfo) =>
            {
                existingInfo.TokenIds.Add(tokenId);
                existingInfo.ExecutionCount++;
                existingInfo.IsActive = existingInfo.IsActive || isExecutable;
                existingInfo.LastExecutionTime = DateTime.UtcNow;
                return existingInfo;
            }
        );
        
        Console.WriteLine($"Tracked node execution: {nodeId} by token {tokenId}, executable: {isExecutable}");
    }
    
    /// <summary>
    /// ثبت اطلاعات اجرای یک فلو
    /// </summary>
    public void TrackFlowExecution(string flowId, Guid sourceTokenId, Guid targetTokenId, bool isExecutable)
    {
        ExecutedFlows.AddOrUpdate(
            flowId,
            new FlowExecutionInfo
            {
                FlowId = flowId,
                SourceTokenIds = sourceTokenId != Guid.Empty ? new List<Guid> { sourceTokenId } : new List<Guid>(),
                TargetTokenIds = targetTokenId != Guid.Empty ? new List<Guid> { targetTokenId } : new List<Guid>(),
                ExecutionCount = 1,
                IsActive = isExecutable,
                LastExecutionTime = DateTime.UtcNow
            },
            (key, existingInfo) =>
            {
                if (sourceTokenId != Guid.Empty)
                    existingInfo.SourceTokenIds.Add(sourceTokenId);
                
                if (targetTokenId != Guid.Empty)
                    existingInfo.TargetTokenIds.Add(targetTokenId);
                
                existingInfo.ExecutionCount++;
                existingInfo.IsActive = existingInfo.IsActive || isExecutable;
                existingInfo.LastExecutionTime = DateTime.UtcNow;
                return existingInfo;
            }
        );
        
        Console.WriteLine($"Tracked flow execution: {flowId}, source token: {sourceTokenId}, target token: {targetTokenId}, executable: {isExecutable}");
    }
    
    /// <summary>
    /// دریافت مسیرهای اجرا شده به همراه فلوها
    /// </summary>
    public List<NodeExecutionInfo> GetExecutedNodes()
    {
        return ExecutedNodes.Values.ToList();
    }
    
    /// <summary>
    /// دریافت فلوهای اجرا شده
    /// </summary>
    public List<FlowExecutionInfo> GetExecutedFlows()
    {
        return ExecutedFlows.Values.ToList();
    }
    
    /// <summary>
    /// دریافت نقشه کلی از فرآیند اجرا شده
    /// </summary>
    public ProcessExecutionMap GetExecutionMap(bool includeVirtualNodesAndFlows = true)
    {
        // فیلتر کردن نودها و فلوهای پیشمایش (ترورسال) بر اساس پارامتر ورودی
        var nodes = includeVirtualNodesAndFlows 
            ? ExecutedNodes.Values.ToList() 
            : ExecutedNodes.Values.Where(n => n.IsActive).ToList();
        
        var flows = includeVirtualNodesAndFlows
            ? ExecutedFlows.Values.ToList()
            : ExecutedFlows.Values.Where(f => f.IsActive).ToList();
        
        return new ProcessExecutionMap
        {
            Nodes = nodes,
            Flows = flows,
            ActiveTokens = Tokens.Where(t => t.Status == TokenStatus.Active).ToList(),
            WaitingTokens = Tokens.Where(t => t.Status == TokenStatus.Waiting).ToList(),
            CompletedTokens = Tokens.Where(t => t.Status == TokenStatus.Completed).ToList(),
            ExpiredTokens = Tokens.Where(t => t.Status == TokenStatus.Expired).ToList(),
            PendingTokens = Tokens.Where(t => t.Status == TokenStatus.PendingToMerge).ToList()
        };
    }
}

/// <summary>
/// اطلاعات اجرای یک نود
/// </summary>
public class NodeExecutionInfo
{
    public string NodeId { get; set; }
    public List<Guid> TokenIds { get; set; } = new();
    public int ExecutionCount { get; set; }
    public bool IsActive { get; set; }
    public DateTime LastExecutionTime { get; set; }
}

/// <summary>
/// اطلاعات اجرای یک فلو
/// </summary>
public class FlowExecutionInfo
{
    public string FlowId { get; set; }
    public List<Guid> SourceTokenIds { get; set; } = new();
    public List<Guid> TargetTokenIds { get; set; } = new();
    public int ExecutionCount { get; set; }
    public bool IsActive { get; set; }
    public DateTime LastExecutionTime { get; set; }
}

/// <summary>
/// نقشه کلی اجرای فرآیند
/// </summary>
public class ProcessExecutionMap
{
    public List<NodeExecutionInfo> Nodes { get; set; }
    public List<FlowExecutionInfo> Flows { get; set; }
    public List<BpmnV3Token> ActiveTokens { get; set; }
    public List<BpmnV3Token> WaitingTokens { get; set; }
    public List<BpmnV3Token> CompletedTokens { get; set; }
    public List<BpmnV3Token> ExpiredTokens { get; set; }
    public List<BpmnV3Token> PendingTokens { get; set; }
}