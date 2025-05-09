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

    // کلید استقرار فرآیند که این نمونه از آن ایجاد شده است
    public string DeploymentKey { get; set; }

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
    public BpmnDefinitionsHandler DefinitionsHandler { get; private set; }

    // افزودن قفل‌های مورد نیاز برای مدیریت همزمانی
    private readonly object _gatewayLockObj = new object();
    private readonly ConcurrentDictionary<string, object> _gatewayLocks = new ConcurrentDictionary<string, object>();

    // فیلد جدید برای ذخیره متغیرهای فرآیند
    private readonly Dictionary<string, object> _variables = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    
    // شناسه فرآیند
    public string Id { get; }

    public BpmnV3ProcessInstance(string processElementId, string definitionXml)
    {
        ProcessElementId = processElementId;
        DefinitionXml = definitionXml;
        DefinitionsHandler = new BpmnDefinitionsHandler(definitionXml);
        ExecutedNodes = new ConcurrentDictionary<string, NodeExecutionInfo>();
        ExecutedFlows = new ConcurrentDictionary<string, FlowExecutionInfo>();
        Id = Guid.NewGuid().ToString();
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

    public BpmnV3Token CreateToken(string elementId, string flowId = null, bool isExecutable = true)
    {
        var newToken = new BpmnV3Token(elementId, flowId);
        
        // تنظیم وضعیت executable بر اساس پارامتر ورودی
        if (!isExecutable)
        {
            newToken.UnExecutable();
        }
        
        Tokens.Add(newToken);
        return newToken;
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
                await HandleExclusiveGateway(token, gateway, isExecutable);
                break;
            case BpmnParallelGateway:
                await HandleParallelGateway(token, gateway, isExecutable);
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

    private async Task HandleExclusiveGateway(BpmnV3Token token, BpmnGateway gateway, bool? isExecutable)
    {
        // ایجاد یا دریافت قفل منحصر به فرد برای این گیت‌وی
        var gatewayLock = _gatewayLocks.GetOrAdd(gateway.id, _ => new object());
        
        // 1. بررسی جریان‌های ورودی
        var incomingFlows = DefinitionsHandler.GetIncomingSequenceFlows(gateway);
        
        // اگر بیش از یک جریان ورودی داریم، نیاز به منطق مرج داریم
        if (incomingFlows.Count > 1)
        {
            Console.WriteLine($"Exclusive gateway {gateway.id} has multiple incoming flows");
            
            // در گیت‌وی Exclusive، فقط اولین توکن را پردازش می‌کنیم و بقیه را نادیده می‌گیریم
            // همین توکن فعلی که اولین توکن است را پردازش می‌کنیم
            
            lock (gatewayLock)
            {
                var tokensAtGateway = Tokens
                    .Where(t => t.CurrentElementId == gateway.id && t.Status == TokenStatus.Active)
                    .ToList();
                
                // اگر توکن‌های دیگری هم در گیت‌وی منتظر هستند، آن‌ها را تکمیل می‌کنیم
                foreach (var otherToken in tokensAtGateway.Where(t => t.Id != token.Id))
                {
                    Console.WriteLine($"Completing token {otherToken.Id} at exclusive gateway {gateway.id} as it arrived after the first token");
                    otherToken.Complete();
                }
            }
        }
        
        // 2. پردازش جریان‌های خروجی
        await ProcessExclusiveGatewayOutgoingFlows(token, gateway, isExecutable);
    }

    private async Task HandleParallelGateway(BpmnV3Token token, BpmnGateway gateway, bool? isExecutable)
    {
        // ایجاد یا دریافت قفل منحصر به فرد برای این گیت‌وی
        var gatewayLock = _gatewayLocks.GetOrAdd(gateway.id, _ => new object());
        
        // قبل از هر کاری، توکن را وارد حالت انتظار می‌کنیم
        token.SetPendingToMerge();

        var incomingFlows = DefinitionsHandler.GetIncomingSequenceFlows(gateway);
        
        // اگر فقط یک مسیر ورودی داریم، نیازی به ادغام نیست
        if (incomingFlows.Count <= 1)
        {
            Console.WriteLine($"Parallel gateway {gateway.id} has only one incoming flow, no need to merge");
            // قفل نمی‌خواهیم چون نیازی به بررسی شرایط ادغام نیست
            await ProcessParallelGatewayOutgoingFlows(token, gateway, isExecutable);
            return;
        }
        
        BpmnV3Token parentTokenToUse = null;
        bool shouldContinue = false;
        bool anyIncomingTokenExecutable = false;
        
        lock (gatewayLock)
        {
            Console.WriteLine($"Evaluating parallel gateway {gateway.id} with token {token.Id}");
            
            // 1. یافتن همه توکن‌های منتظر در گیت‌وی
            var tokensAtGateway = Tokens
                .Where(t => t.CurrentElementId == gateway.id && t.Status == TokenStatus.PendingToMerge)
                .ToList();
            
            Console.WriteLine($"Parallel gateway {gateway.id} has {tokensAtGateway.Count} tokens waiting out of {incomingFlows.Count} expected flows");
            
            // 2. بررسی آیا توکن‌ها از تمام مسیرهای ورودی رسیده‌اند
            var receivedFlows = new HashSet<string>();
            foreach (var t in tokensAtGateway)
            {
                var lastHistoryEntry = t.History
                    .OrderByDescending(h => h.Timestamp)
                    .FirstOrDefault();
                    
                if (lastHistoryEntry != null && !string.IsNullOrEmpty(lastHistoryEntry.FlowId))
                {
                    receivedFlows.Add(lastHistoryEntry.FlowId);
                }
            }
            
            // بررسی آیا از همه مسیرهای ورودی، توکن دریافت شده است
            var allPathsReceived = receivedFlows.Count == incomingFlows.Count;
            
            if (allPathsReceived)
            {
                Console.WriteLine($"All paths received for parallel gateway {gateway.id}. Proceeding with merge.");
                
                // بررسی آیا حداقل یکی از توکن‌های ورودی فعال است
                anyIncomingTokenExecutable = tokensAtGateway.Any(t => t.IsExecutable);
                
                // تکمیل همه توکن‌های موجود در گیت‌وی
                foreach (var t in tokensAtGateway)
                {
                    t.Complete();
                    Console.WriteLine($"Completed token {t.Id} in gateway {gateway.id}");
                }

                // یافتن توکن برای ادامه مسیر (توکن والد یا فعلی)
                parentTokenToUse = token.ParentTokenId != null
                    ? Tokens.FirstOrDefault(t => t.Id == token.ParentTokenId)
                    : token;
                    
                if (parentTokenToUse != null)
                {
                    if (parentTokenToUse.Status == TokenStatus.Completed || 
                        parentTokenToUse.Status == TokenStatus.PendingToMerge ||
                        parentTokenToUse.Status == TokenStatus.Expired)
                    {
                    parentTokenToUse.Reactivate();
                    }
                    
                    // Set token executable if any incoming token was executable
                    if (anyIncomingTokenExecutable && !parentTokenToUse.IsExecutable)
                    {
                        parentTokenToUse.Executable();
                    }
                    
                    Console.WriteLine($"Using token {parentTokenToUse.Id} to proceed from gateway {gateway.id}. Any incoming executable: {anyIncomingTokenExecutable}");
                    shouldContinue = true;
                }
                else
                {
                    Console.WriteLine($"Could not determine a token to proceed from parallel gateway {gateway.id}. Fallback to current token {token.Id}");
                    parentTokenToUse = token;
                    if (parentTokenToUse.Status != TokenStatus.Active) parentTokenToUse.Reactivate();
                    shouldContinue = true;
                }
            }
            else
            {
                Console.WriteLine($"Waiting for more tokens at parallel gateway {gateway.id}. Received {receivedFlows.Count}/{incomingFlows.Count} flows.");
                
                // ثبت ورود به گیت‌وی و انتظار
                TrackNodeExecution(gateway.id, token.Id, token.IsExecutable);
                shouldContinue = false;
            }
        }
        
        if (shouldContinue && parentTokenToUse != null)
        {
            await ProcessParallelGatewayOutgoingFlows(parentTokenToUse, gateway, anyIncomingTokenExecutable);
        }
    }
    
    // پردازش جریان‌های خروجی گیت‌وی Parallel
    private async Task ProcessParallelGatewayOutgoingFlows(BpmnV3Token token, BpmnGateway gateway, bool? isExecutable)
    {
        Console.WriteLine($"Processing {DefinitionsHandler.GetOutgoingSequenceFlows(gateway).Count} outgoing flows for parallel gateway {gateway.id}");
        // در گیت‌وی موازی، تمام مسیرهای خروجی به صورت موازی اجرا می‌شوند
        
        // بررسی وضعیت اجرایی توکن فعلی
        bool tokenIsExecutable = isExecutable ?? token.IsExecutable;
        
        var outgoingFlows = DefinitionsHandler.GetOutgoingSequenceFlows(gateway);
        
        // اگر هیچ مسیر خروجی وجود ندارد، توکن را منقضی کن
        if (outgoingFlows.Count == 0)
        {
            Console.WriteLine($"No outgoing flows for parallel gateway {gateway.id}. Token will expire.");
            token.Expire();
            return;
        }
        
        // برای هر مسیر خروجی، یک توکن جدید ایجاد می‌کنیم
        foreach (var flow in outgoingFlows)
        {
            // ثبت استفاده از فلو
            TrackFlowExecution(flow.id, token.Id, Guid.Empty, tokenIsExecutable);
            
            if (tokenIsExecutable)
            {
                Console.WriteLine($"Creating token for flow {flow.id}, executable: {tokenIsExecutable}");
                // ایجاد توکن جدید و انتقال به المان بعدی
                var newToken = CreateToken(flow.targetRef, flow.id, tokenIsExecutable);
                    newToken.ParentTokenId = token.Id;
                
                // ثبت ایجاد توکن جدید
                TrackNodeExecution(flow.targetRef, newToken.Id, tokenIsExecutable);
                TrackFlowExecution(flow.id, Guid.Empty, newToken.Id, tokenIsExecutable);
                
                Console.WriteLine($"Created {(tokenIsExecutable ? "executable" : "non-executable")} token {newToken.Id} to {flow.targetRef}");
            }
            else
            {
                // ایجاد توکن غیر اجرایی برای نمایش
                    var inactiveToken = CreateUnExecutableToken(flow.targetRef, flow.id);
                TrackNodeExecution(flow.targetRef, inactiveToken.Id, false);
                    inactiveToken.ParentTokenId = token.Id;
                }
            }
        
        // توکن اصلی را تکمیل می‌کنیم چون کار آن تمام شده است
        token.Complete();
    }

    private async Task HandleInclusiveGateway(BpmnV3Token token, BpmnGateway gateway, bool? isExecutable)
    {
        // ایجاد یا دریافت قفل منحصر به فرد برای این گیت‌وی
        var gatewayLock = _gatewayLocks.GetOrAdd(gateway.id, _ => new object());
        
        // توکن را به حالت انتظار تبدیل می‌کنیم
        token.SetPendingToMerge();
        
        // دریافت لیست فلوهای ورودی
        var incomingFlows = DefinitionsHandler.GetIncomingSequenceFlows(gateway);
        
        // اگر فقط یک مسیر ورودی داریم، نیازی به ادغام نیست
        if (incomingFlows.Count <= 1)
        {
            Console.WriteLine($"Inclusive gateway {gateway.id} has only one incoming flow, no need to merge.");
            await ProcessInclusiveGatewayOutgoingFlows(token, gateway, isExecutable);
            return;
        }
        
        // متغیرهای مورد نیاز برای تصمیم‌گیری در مورد مرج
        BpmnV3Token parentTokenToUse = null;
        bool shouldContinue = false;
        bool anyIncomingTokenExecutable = false;
        
        // اعمال قفل برای جلوگیری از تداخل در پردازش همزمان توکن‌ها
        lock (gatewayLock)
        {
            Console.WriteLine($"Evaluating inclusive gateway {gateway.id} with token {token.Id}");
            
            // یافتن توکن‌های منتظر در گیت‌وی
            var tokensAtGateway = Tokens
                .Where(t => t.CurrentElementId == gateway.id && t.Status == TokenStatus.PendingToMerge)
                .ToList();
            
            // بررسی ساده: آیا تعداد توکن‌های منتظر برابر با تعداد مسیرهای ورودی است؟
            var canMerge = tokensAtGateway.Count >= incomingFlows.Count;
            
            Console.WriteLine($"Inclusive Gateway {gateway.id}: Tokens waiting: {tokensAtGateway.Count}, Incoming flows: {incomingFlows.Count}, Can merge: {canMerge}");
            
            if (canMerge)
            {
                Console.WriteLine($"All required tokens arrived at inclusive gateway {gateway.id}. Proceeding with merge.");
                
                // بررسی آیا حداقل یکی از توکن‌های ورودی فعال است
                anyIncomingTokenExecutable = tokensAtGateway.Any(t => t.IsExecutable);
                
                // تکمیل همه توکن‌های منتظر
                foreach (var t in tokensAtGateway)
                {
                    t.Complete();
                    Console.WriteLine($"Completed token {t.Id} in gateway {gateway.id}");
                }
                
                // یافتن توکن برای ادامه مسیر (توکن والد یا فعلی)
                parentTokenToUse = token.ParentTokenId != null 
                    ? Tokens.FirstOrDefault(t => t.Id == token.ParentTokenId) 
                    : token;
                
                if (parentTokenToUse != null)
                {
                    // اگر توکن والد در وضعیت غیرفعال است، آن را فعال می‌کنیم
                    if (parentTokenToUse.Status != TokenStatus.Active)
                    {
                        parentTokenToUse.Reactivate();
                    }
                    
                    // اگر حداقل یکی از توکن‌های ورودی فعال بود، توکن خروجی را نیز فعال می‌کنیم
                    if (anyIncomingTokenExecutable && !parentTokenToUse.IsExecutable)
                    {
                        parentTokenToUse.Executable();
                    }
                    
                    Console.WriteLine($"Using token {parentTokenToUse.Id} to proceed from gateway {gateway.id}");
                    shouldContinue = true;
            }
            else
            {
                    // اگر توکن والد پیدا نشد، از توکن فعلی استفاده می‌کنیم
                    Console.WriteLine($"No parent token found, using current token {token.Id}");
                    parentTokenToUse = token;
                    
                    // اطمینان از فعال بودن توکن
                    if (parentTokenToUse.Status != TokenStatus.Active)
                    {
                        parentTokenToUse.Reactivate();
                    }
                    
                    // تنظیم وضعیت اجرایی توکن
                    if (anyIncomingTokenExecutable && !parentTokenToUse.IsExecutable)
                    {
                        parentTokenToUse.Executable();
                    }
                    
                    shouldContinue = true;
                }
        }
        else
            {
                // هنوز به تعداد کافی توکن دریافت نشده است
                Console.WriteLine($"Waiting for more tokens at inclusive gateway {gateway.id}. Received {tokensAtGateway.Count}/{incomingFlows.Count}");
                shouldContinue = false;
            }
        }
        
        // اگر شرایط مرج فراهم شده، مسیرهای خروجی را پردازش می‌کنیم
        if (shouldContinue && parentTokenToUse != null)
        {
            await ProcessInclusiveGatewayOutgoingFlows(parentTokenToUse, gateway, anyIncomingTokenExecutable);
        }
    }

    /// <summary>
    /// تعیین جریان‌های ورودی فعال برای یک گیت‌وی مشمول (Inclusive)
    /// مطابق با مشخصات BPMN 2.0، یک جریان ورودی فعال جریانی است که:
    /// 1. یا دارای یک توکن موجود است
    /// 2. یا دارای یک مسیر فعال از یک توکن موجود به گیت‌وی است
    /// </summary>
    private HashSet<string> DetermineActiveIncomingFlows(string gatewayId, List<BpmnSequenceFlow> incomingFlows)
    {
        var activeFlows = new HashSet<string>();
        
        // 1. بررسی مستقیم: آیا این فلو توسط موتور اجرا شده و فعال بوده است؟
        foreach (var flow in incomingFlows)
        {
            // بررسی اگر این فلو در سیستم ردیابی ما فعال است
            if (ExecutedFlows.TryGetValue(flow.id, out var flowInfo) && flowInfo.IsActive)
            {
                activeFlows.Add(flow.id);
                continue;
            }
            
            // 2. بررسی آیا توکن‌های منتظری روی ورودی یا خروجی این فلو وجود دارند
            var tokensOnFlow = Tokens
                .Where(t => t.History.Any(h => h.FlowId == flow.id) &&
                          (t.Status == TokenStatus.Active || t.Status == TokenStatus.Waiting))
                .ToList();
                
            if (tokensOnFlow.Any())
            {
                activeFlows.Add(flow.id);
                continue;
            }
            
            // 3. بررسی آیا توکن‌های منتظری در گیت‌وی هست که از این فلو آمده‌اند
            var tokensAtGatewayFromThisFlow = Tokens
                .Where(t => t.CurrentElementId == gatewayId && t.Status == TokenStatus.PendingToMerge)
                .Where(t => {
                    var lastHistoryEntry = t.History
                        .OrderByDescending(h => h.Timestamp)
                        .FirstOrDefault();
                    return lastHistoryEntry?.FlowId == flow.id;
                })
                .ToList();
                
            if (tokensAtGatewayFromThisFlow.Any())
            {
                activeFlows.Add(flow.id);
                continue;
            }
            
            // 4. بررسی آیا منبع این فلو دارای توکن فعال است
            // این برای رویدادهای مرزی مهم است
            var sourceElementId = flow.sourceRef;
            var tokensOnSourceElement = Tokens
                .Where(t => t.CurrentElementId == sourceElementId &&
                          (t.Status == TokenStatus.Active || t.Status == TokenStatus.Waiting))
                .ToList();
                
            if (tokensOnSourceElement.Any())
            {
                activeFlows.Add(flow.id);
                continue;
            }
        }
        
        // 5. بررسی روند تایمر: آیا هر یک از فلوهای ورودی از رویدادهای تایمر می‌آیند؟
        foreach (var flow in incomingFlows.Where(flow => !activeFlows.Contains(flow.id)))
        {
            var sourceElement = DefinitionsHandler.GetElementById(flow.sourceRef);
            if (sourceElement is BpmnBoundaryEvent boundaryEvent)
            {
                // اگر یک رویداد مرزی تایمر است، آن را فعال در نظر بگیر
                if (boundaryEvent.Items?.OfType<BpmnTimerEventDefinition>().Any() == true)
                {
                    var attachedToElement = DefinitionsHandler.GetElementById(boundaryEvent.attachedToRef?.Name);
                    
                    // اگر المان متصل شده نیز دارای توکن فعال است، فلو را فعال در نظر بگیر
                    if (attachedToElement != null)
                    {
                        var activeTokensOnAttachedElement = Tokens
                            .Where(t => t.CurrentElementId == attachedToElement.id && 
                                      (t.Status == TokenStatus.Active || t.Status == TokenStatus.Waiting))
                            .Any();
                            
                        if (activeTokensOnAttachedElement)
                        {
                            activeFlows.Add(flow.id);
                            continue;
                        }
                    }
                }
            }
            
            // اگر این یک رویداد تایمر میانی است، آن را فعال در نظر بگیر
            if (sourceElement is BpmnIntermediateCatchEvent intermediateEvent)
            {
                if (intermediateEvent.Items?.OfType<BpmnTimerEventDefinition>().Any() == true)
                {
                    // بررسی کن آیا توکنی که مسیر آن از این رویداد میانی می‌گذرد وجود دارد
                    var tokensForEvent = Tokens
                        .Where(t => t.History.Any(h => h.ElementId == intermediateEvent.id))
                        .ToList();
                        
                    if (tokensForEvent.Any())
                    {
                        activeFlows.Add(flow.id);
                        continue;
                    }
                }
            }
        }
        
        // اگر هیچ فلوی فعالی شناسایی نشد، مقادیر پیش‌فرض را بررسی کن
        if (activeFlows.Count == 0)
        {
            // بررسی اگر حداقل یک توکن در مسیرهای ورودی وجود دارد
            var anyTokensOnIncomingPaths = Tokens
                .Where(t => t.History.Any(h => incomingFlows.Any(flow => flow.id == h.FlowId)) &&
                          (t.Status == TokenStatus.Active || t.Status == TokenStatus.Waiting))
                .Any();
                
            if (anyTokensOnIncomingPaths)
            {
                // اگر حداقل یک توکن روی مسیرهای ورودی وجود دارد،
                // همه فلوهای ورودی را فعال در نظر بگیر
                foreach (var flow in incomingFlows)
                {
                    activeFlows.Add(flow.id);
                }
            }
        }
        
        return activeFlows;
    }

    /// <summary>
    /// بررسی آیا مسیری از رویدادهای مرزی (boundary events) فعال به جریان مشخص شده وجود دارد
    /// این برای تشخیص رویدادهای مرزی غیرمتوقف‌کننده که مسیرهای موازی ایجاد می‌کنند مهم است
    /// </summary>
    private bool IsBoundaryEventPathActive(string flowId, HashSet<string> visitedElements)
    {
        if (visitedElements.Contains(flowId))
        {
            return false;
        }
        
        visitedElements.Add(flowId);
        
        var flow = DefinitionsHandler.GetElementById(flowId) as BpmnSequenceFlow;
        if (flow == null) return false;
        
        var sourceElement = DefinitionsHandler.GetElementById(flow.sourceRef);
        if (sourceElement == null) return false;
        
        // Check if the source element is a boundary event
        if (sourceElement is BpmnBoundaryEvent boundaryEvent)
        {
            // Check if there are active tokens on the element to which the boundary event is attached
            var attachedElement = DefinitionsHandler.GetElementById(boundaryEvent.attachedToRef?.Name);
            if (attachedElement != null)
            {
                var activeTokensOnAttachedElement = Tokens
                    .Where(t => t.CurrentElementId == attachedElement.id && 
                               (t.Status == TokenStatus.Active || t.Status == TokenStatus.Waiting))
                    .Any();
                
                // If this is a non-interrupting boundary event and there are active tokens
                // on the attached element, then this path is active
                if (activeTokensOnAttachedElement && boundaryEvent.cancelActivity == false)
                {
                    return true;
                }
            }
        }
        
        // Check recursively for boundary events in the path
        var incomingFlowsToSource = DefinitionsHandler.GetIncomingSequenceFlows(sourceElement);
        foreach (var incomingFlow in incomingFlowsToSource)
        {
            if (IsBoundaryEventPathActive(incomingFlow.id, new HashSet<string>(visitedElements)))
            {
                return true;
            }
        }
        
        return false;
    }

    /// <summary>
    /// بررسی می‌کند آیا مسیری فعال از یک توکن موجود به جریان مشخص شده وجود دارد
    /// </summary>
    private bool IsPathActiveToFlow(string flowId, HashSet<string> visitedElements)
    {
        // بررسی حلقه بی‌نهایت
        if (visitedElements.Contains(flowId))
        {
            return false;
        }
        
        visitedElements.Add(flowId);
        
        var flow = DefinitionsHandler.GetElementById(flowId) as BpmnSequenceFlow;
        if (flow == null) return false;
        
        var sourceElement = DefinitionsHandler.GetElementById(flow.sourceRef);
        if (sourceElement == null) return false;
        
        // بررسی مستقیم: آیا توکن فعالی در عنصر منبع وجود دارد؟
        var activeTokenAtSource = Tokens.Any(t => 
            t.CurrentElementId == sourceElement.id && 
            (t.Status == TokenStatus.Active || t.Status == TokenStatus.Waiting));
            
        if (activeTokenAtSource)
        {
            return true;
        }
        
        // بررسی بازگشتی: آیا مسیر فعالی به عنصر منبع وجود دارد؟
        var incomingFlowsToSource = DefinitionsHandler.GetIncomingSequenceFlows(sourceElement);
        
        foreach (var incomingFlow in incomingFlowsToSource)
        {
            if (IsPathActiveToFlow(incomingFlow.id, new HashSet<string>(visitedElements)))
            {
                return true;
            }
        }

        return false;
    }

    // بهبود متد پردازش مسیرهای خروجی برای گیت‌وی Inclusive
    private async Task ProcessInclusiveGatewayOutgoingFlows(BpmnV3Token token, BpmnGateway gateway, bool? isExecutable)
    {
        if (token == null)
        {
            Console.WriteLine("Warning: Null token passed to ProcessInclusiveGatewayOutgoingFlows");
            return;
        }

        // بررسی وضعیت اجرایی توکن
        bool tokenIsExecutable = isExecutable ?? token.IsExecutable;
        
        // دریافت لیست فلوهای خروجی
        var outgoingFlows = DefinitionsHandler.GetOutgoingSequenceFlows(gateway);
        Console.WriteLine($"Processing {outgoingFlows.Count} outgoing flows for inclusive gateway {gateway.id}");

        // اگر هیچ مسیر خروجی نداریم، توکن را منقضی می‌کنیم
        if (outgoingFlows.Count == 0)
        {
            Console.WriteLine($"No outgoing flows for inclusive gateway {gateway.id}. Token will expire.");
            token.Expire();
            return;
        }

        // یافتن مسیر پیش‌فرض (در صورت وجود)
        BpmnSequenceFlow defaultFlow = null;
        if (gateway is BpmnInclusiveGateway inclusiveGateway && !string.IsNullOrEmpty(inclusiveGateway.@default))
        {
            defaultFlow = outgoingFlows.FirstOrDefault(f => f.id == inclusiveGateway.@default);
        }

        // لیست مسیرهایی که شرط آنها برقرار است
        var selectedFlows = new List<BpmnSequenceFlow>();
        
        // بررسی شرط هر مسیر خروجی
        foreach (var flow in outgoingFlows)
        {
            // مسیر پیش‌فرض را در ارزیابی شرط‌ها نادیده می‌گیریم
            if (defaultFlow != null && flow.id == defaultFlow.id)
            {
                continue;
            }

            // ارزیابی شرط مسیر
            bool conditionMet = await DefinitionsHandler.EvaluateCondition(flow, token, this);
            Console.WriteLine($"Evaluating flow {flow.id} with condition met: {conditionMet}");

            // اگر شرط برقرار است، مسیر را اضافه می‌کنیم
            if (conditionMet)
            {
                selectedFlows.Add(flow);
            }
        }

        // اگر هیچ شرطی برقرار نیست و مسیر پیش‌فرض داریم، از آن استفاده می‌کنیم
        if (selectedFlows.Count == 0 && defaultFlow != null)
        {
            Console.WriteLine($"No conditions met, using default flow {defaultFlow.id}");
            selectedFlows.Add(defaultFlow);
        }

        // اگر هیچ مسیری انتخاب نشده، توکن را منقضی می‌کنیم
        if (selectedFlows.Count == 0)
        {
            Console.WriteLine($"No outgoing flow conditions were met in inclusive gateway {gateway.id}. Token will expire.");
            token.Expire();
            return;
        }

        // پردازش مسیرهای انتخاب شده
        foreach (var flow in selectedFlows)
        {
            // ثبت استفاده از فلو
            TrackFlowExecution(flow.id, token.Id, Guid.Empty, tokenIsExecutable);
            
            if (tokenIsExecutable)
            {
                // ایجاد توکن جدید و فعال
                var newToken = CreateToken(flow.targetRef, flow.id, tokenIsExecutable);
                    newToken.ParentTokenId = token.Id;
                
                // ثبت ایجاد توکن جدید
                TrackNodeExecution(flow.targetRef, newToken.Id, tokenIsExecutable);
                
                    Console.WriteLine($"Created executable token {newToken.Id} to {flow.targetRef}");
            }
            else
            {
                // ایجاد توکن غیرفعال برای نمایش
                    var inactiveToken = CreateUnExecutableToken(flow.targetRef, flow.id);
                    inactiveToken.ParentTokenId = token.Id;
                
                    Console.WriteLine($"Created non-executable token {inactiveToken.Id} to {flow.targetRef}");
                }
            }

        // برای مسیرهای غیرانتخابی، توکن‌های غیرفعال ایجاد می‌کنیم
        foreach (var flow in outgoingFlows.Where(f => !selectedFlows.Contains(f)))
        {
            // ثبت جریان به صورت غیرفعال
            TrackFlowExecution(flow.id, token.Id, Guid.Empty, false);
            
            // ایجاد توکن غیرفعال
            var inactiveToken = CreateUnExecutableToken(flow.targetRef, flow.id);
            inactiveToken.ParentTokenId = token.Id;
            
            Console.WriteLine($"Created non-executable token {inactiveToken.Id} to {flow.targetRef} (condition not met)");
        }
        
        // توکن اصلی را تکمیل می‌کنیم
        token.Complete();
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
        
        // اگر شرط ادغام برقار است، توکن‌ها را ادغام می‌کنیم
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

    // پردازش جریان‌های خروجی گیت‌وی Exclusive
    private async Task ProcessExclusiveGatewayOutgoingFlows(BpmnV3Token token, BpmnGateway gateway, bool? isExecutable)
    {
        // بررسی وضعیت اجرایی توکن فعلی
        bool tokenIsExecutable = isExecutable ?? token.IsExecutable;
        
        var outgoingFlows = DefinitionsHandler.GetOutgoingSequenceFlows(gateway);
        
        // اگر توکن غیرفعال است، تمام مسیرهای خروجی را به صورت غیرفعال رهگیری کن
        if (!tokenIsExecutable)
        {
            Console.WriteLine($"Non-executable token {token.Id} processing exclusive gateway {gateway.id}");
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
        BpmnSequenceFlow selectedFlow = null;
        
        foreach (var flow in outgoingFlows)
        {
            // مسیر پیش‌فرض را در ارزیابی شرط‌ها نادیده می‌گیریم
            if (defaultFlow != null && flow.id == defaultFlow.id)
            {
                continue;
            }

            bool conditionMet = await DefinitionsHandler.EvaluateCondition(flow, token, this);
            Console.WriteLine($"Evaluating flow {flow.id} with condition met: {conditionMet}, executable: {token.IsExecutable}");

            if (conditionMet)
            {
                selectedFlow = flow;
                break;
            }
        }
        
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
            
            // انتقال توکن به المان بعدی
            token.MoveTo(selectedFlow.targetRef, selectedFlow.id);
            
            // ثبت نود جدید
            TrackNodeExecution(selectedFlow.targetRef, token.Id, true);
            
            // برای مسیرهای غیرانتخابی، ایجاد توکن غیراجرایی برای نمایش
            foreach (var flow in outgoingFlows.Where(f => f.id != selectedFlow.id))
            {
                // ثبت جریان و نود به صورت غیرفعال برای نمایش
                TrackFlowExecution(flow.id, token.Id, Guid.Empty, false);
                var inactiveToken = CreateUnExecutableToken(flow.targetRef, flow.id);
                inactiveToken.ParentTokenId = token.Id;
            }
        }
        else
        {
            // هیچ مسیری (حتی پیش‌فرض) پیدا نشد
            Console.WriteLine($"No valid outgoing flow found for exclusive gateway {gateway.id}. Token will expire.");
            token.Expire();
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

    public void AddToken(BpmnV3Token token)
    {
        if (token == null) throw new ArgumentNullException(nameof(token));
        
        // Add the token to the collection
        Tokens.Add(token);
        
        // Track token in node execution
        TrackNodeExecution(token.CurrentElementId, token.Id, token.IsExecutable);
        
        // Track flow execution if this token was created from a flow
        var lastHistory = token.History.OrderByDescending(h => h.Timestamp).FirstOrDefault();
        if (lastHistory != null && !string.IsNullOrEmpty(lastHistory.FlowId))
        {
            TrackFlowExecution(lastHistory.FlowId, token.ParentTokenId ?? Guid.Empty, token.Id, token.IsExecutable);
        }
    }

    /// <summary>
    /// تنظیم یک متغیر فرآیند
    /// </summary>
    public void SetVariable(string name, object value)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Name cannot be null or empty.", nameof(name));
        }
        
        lock (_variables)
        {
            _variables[name] = value;
        }
    }
    
    /// <summary>
    /// دریافت مقدار یک متغیر فرآیند
    /// </summary>
    public object GetVariable(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Name cannot be null or empty.", nameof(name));
        }
        
        lock (_variables)
        {
            return _variables.TryGetValue(name, out var value) ? value : null;
        }
    }
    
    /// <summary>
    /// دریافت تمام متغیرهای فرآیند
    /// </summary>
    public Dictionary<string, object> GetAllVariables()
    {
        lock (_variables)
        {
            return new Dictionary<string, object>(_variables);
        }
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