using Novin.Bpmn;
using Novin.Bpmn.Core;
using Novin.Bpmn.Models;
using Novin.Bpmn.V3;
using Novin.Bpmn.V3.Events;
using Novin.Bpmn.V3.Handlers.Gateways;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;

public class BpmnV3ProcessExecutor
{
    private readonly BpmnV3ProcessInstance _processInstance;
    private readonly ScriptHandler _scriptHandler;
    private readonly Dictionary<Type, IBpmnEventHandler> _eventHandlers = new Dictionary<Type, IBpmnEventHandler>();
    private readonly BpmnV3GatewayRouter _gatewayRouter;

    public BpmnV3ProcessExecutor(BpmnV3ProcessInstance processInstance, BpmnV3GatewayRouter gatewayRouter = null)
    {
        _processInstance = processInstance;
        _scriptHandler = new ScriptHandler();
        
        // Initialize gateway router
        _gatewayRouter = gatewayRouter ?? InitializeDefaultGatewayRouter();

        // Register event handlers
        RegisterEventHandlers();

        if (!_processInstance.Tokens.Any())
        {
            var startEvent = FindStartEvent();
            if (startEvent == null)
            {
                throw new InvalidOperationException("No start event found in the process definition.");
            }

            // Create the first token at the start event
            var startEventId = startEvent.id;
            _processInstance.CreateToken(startEventId);
        }
    }
    
    // Create a default gateway router when none is provided
    private BpmnV3GatewayRouter InitializeDefaultGatewayRouter()
    {
        return new BpmnV3GatewayRouter(
            new BpmnV3ExclusiveGatewayHandler(_scriptHandler),
            new BpmnV3InclusiveGatewayHandler(_scriptHandler),
            new BpmnV3ParallelGatewayHandler()
        );
    }

    // Register all event handlers
    private void RegisterEventHandlers()
    {
        // Register error event handler
        var errorHandler = new Novin.Bpmn.V3.Events.ErrorEventHandler(_processInstance);
        _eventHandlers[typeof(BpmnErrorEventDefinition)] = errorHandler;
        
        // Register timer event handler
        var timerHandler = new TimerEventHandler(_processInstance);
        _eventHandlers[typeof(BpmnTimerEventDefinition)] = timerHandler;
        
        // Register legacy adapter for backward compatibility
        var legacyAdapter = new LegacyEventAdapter(_processInstance);
        _eventHandlers[typeof(BpmnSignalEventDefinition)] = legacyAdapter; // Support for other event types
        
        // Initialize all handlers
        Task.WhenAll(_eventHandlers.Values.Select(h => h.Initialize())).GetAwaiter().GetResult();
    }

    // Handle gateways using the dual token strategy
    private async Task<List<BpmnV3Token>> HandleGatewayAsync(BpmnGateway gateway, BpmnV3Token token)
    {
        Console.WriteLine($"Handling gateway {gateway.id} with token {token.Id}");
        return await _gatewayRouter.RouteTokenAsync(gateway, token, _processInstance);
    }

    // متد اجرای وظیفه اسکریپت
    private async Task ExecuteScriptTask(BpmnScriptTask scriptTask, BpmnV3Token token)
    {
        if (scriptTask.script == null || string.IsNullOrWhiteSpace(scriptTask.script.InnerText))
        {
            Console.WriteLine($"Script task {scriptTask.id} has no script defined.");
            return;
        }

        try
        {
            Console.WriteLine($"Executing script for task {scriptTask.id}");
            var globals = new BpmnV3ScriptGlobals { Instance = _processInstance };
            await _scriptHandler.ExecuteScriptAsync(scriptTask.script.InnerText, globals);
        }
        catch (Exception ex)
        {
            throw new BpmnExecutionException($"Error executing script in task {scriptTask.id}: {ex.Message}", ex);
        }
    }

    // متد اجرای وظیفه سرویس
    private async Task ExecuteServiceTask(BpmnServiceTask serviceTask, BpmnV3Token token)
    {
        // در نسخه فعلی، فقط لاگ می‌کنیم - پیاده‌سازی کامل نیاز به توسعه بیشتر دارد
        Console.WriteLine($"Executing service task {serviceTask.id}");
        
        // در پیاده‌سازی کامل، می‌توان از implementation و operation برای فراخوانی سرویس استفاده کرد
        if (!string.IsNullOrEmpty(serviceTask.implementation))
        {
            Console.WriteLine($"Service implementation: {serviceTask.implementation}");
        }

        if (serviceTask.operationRef != null)
        {
            Console.WriteLine($"Service operation: {serviceTask.operationRef.Name}");
        }

        await Task.CompletedTask; // برای رعایت قرارداد async
    }

    // Start the execution process
    public async Task<BpmnV3ProcessInstance> StartProcessAsync()
    {
        // Process tokens in a loop
        try
        {
            // محدودیت تعداد تکرارها برای جلوگیری از حلقه بی‌نهایت
            int maxIterations = 100; // محدودیت منطقی برای تعداد تکرارها
            int iterations = 0;

            while (_processInstance.Tokens.Any(t => t.Status == TokenStatus.Active) && iterations < maxIterations)
            {
                iterations++;
                
                // ایجاد یک کپی از توکن‌های فعال برای جلوگیری از تغییر در حین حلقه
                var activeTokens = _processInstance.Tokens
                    .Where(t => t.Status == TokenStatus.Active)
                    .ToList();
                    
                if (!activeTokens.Any())
                {
                    Console.WriteLine("No active tokens found. Execution completed.");
                    break;
                }

                Console.WriteLine($"Processing iteration {iterations} with {activeTokens.Count} active tokens");

                // Handle active tokens
                foreach (var token in activeTokens)
                {
                    if (token.Status != TokenStatus.Active)
                    {
                        Console.WriteLine($"Token {token.Id} is no longer active, skipping processing.");
                        continue;
                    }

                    if (token.IsExecutable)
                    {
                        Console.WriteLine(
                            $"Processing token at element {token.Id} {token.CurrentElementId} : {token.IsExecutable}");
                    }

                    var attachedEvents = _processInstance.DefinitionsHandler.GetAttachedEvents(token.CurrentElementId);

                    // Initialize and store boundary events in the dictionary
                    foreach (var boundaryEvent in attachedEvents)
                    {
                        foreach (var eventDefinition in boundaryEvent.Items)
                        {
                            // Determine if the event is interrupting
                            bool isInterrupting = boundaryEvent.cancelActivity == null || boundaryEvent.cancelActivity;
                            
                            // Find the appropriate handler for this event type
                            if (_eventHandlers.TryGetValue(eventDefinition.GetType(), out var handler))
                            {
                                // Register the event with the handler
                                await handler.RegisterEvent(eventDefinition, boundaryEvent, token, isInterrupting);
                                Console.WriteLine($"Registered event {eventDefinition.GetType().Name} for element {token.CurrentElementId}");
                            }
                            else
                            {
                                Console.WriteLine($"No handler found for event type {eventDefinition.GetType().Name}");
                            }
                        }
                    }

                    try
                    {
                        // اجرای عملیات نود فعلی
                        // بررسی نوع المان جاری و اجرای عملیات مناسب
                        var currentElement = _processInstance.DefinitionsHandler.GetElementById(token.CurrentElementId);
                        
                        if (currentElement is BpmnScriptTask scriptTask)
                        {
                            // اجرای اسکریپت‌های درون وظیفه اسکریپت
                            await ExecuteScriptTask(scriptTask, token);
                        }
                        else if (currentElement is BpmnServiceTask serviceTask)
                        {
                            // اجرای وظیفه سرویس (در صورت پیاده‌سازی)
                            await ExecuteServiceTask(serviceTask, token);
                        }
                        else if (currentElement is Novin.Bpmn.Models.BpmnTask task)
                        {
                            // اجرای وظیفه ساده
                            Console.WriteLine($"Executing task {task.id}");
                        }
                        else if (currentElement is BpmnEndEvent endEvent)
                        {
                            // بررسی نوع رویداد پایان و اجرای عملیات مناسب
                            Console.WriteLine($"Reached end event {endEvent.id}");
                            
                            // بررسی اگر رویداد پایان حاوی رویداد خطا است
                            if (endEvent.Items != null && endEvent.Items.OfType<BpmnErrorEventDefinition>().Any())
                            {
                                // ایجاد رویداد خطا برای انتشار به سطوح بالاتر
                                var errorEvent = endEvent.Items.OfType<BpmnErrorEventDefinition>().First();
                                var errorCode = errorEvent.errorRef?.Name ?? "UNKNOWN_ERROR";
                                throw new BpmnExecutionException($"Error event triggered: {errorEvent.id}", errorCode);
                            }
                        }
                        else if (currentElement is BpmnGateway gateway)
                        {
                            // Process gateway using dual token strategy
                            var newTokens = await HandleGatewayAsync(gateway, token);
                            
                            // Add new tokens to the process instance
                            foreach (var newToken in newTokens)
                            {
                                _processInstance.AddToken(newToken);
                            }
                            
                            // Skip automatic advancement since gateway was explicitly handled
                            continue;
                        }
                    }
                    catch (BpmnExecutionException e)
                    {
                        // انتشار خطای خاص BPMN به هندلرهای خطا
                        Console.WriteLine($"BPMN Error: {e.Message}, Error Code: {e.ErrorCode}");
                        
                        // Try to trigger error events for this token
                        bool errorHandled = false;
                        if (_eventHandlers.TryGetValue(typeof(BpmnErrorEventDefinition), out var errorHandler))
                        {
                            errorHandled = await errorHandler.TriggerEvents(token.Id);
                            
                            // If error was handled specifically by ErrorEventHandler, try to match error code
                            if (!errorHandled && errorHandler is Novin.Bpmn.V3.Events.ErrorEventHandler typedErrorHandler)
                            {
                                errorHandled = await typedErrorHandler.TriggerEventsForErrorCode(token.Id, e.ErrorCode);
                            }
                        }
                        
                        if (!errorHandled)
                        {
                            // Re-throw if no error event handler could handle it
                            throw;
                        }
                    }
                    catch (Exception e)
                    {
                        // General exception handling - try to trigger error events as a fallback
                        Console.WriteLine($"Error during token execution: {e.Message}");
                        Console.WriteLine(e.StackTrace);
                        
                        bool errorHandled = false;
                        if (_eventHandlers.TryGetValue(typeof(BpmnErrorEventDefinition), out var errorHandler))
                        {
                            errorHandled = await errorHandler.TriggerEvents(token.Id);
                        }
                        
                        if (!errorHandled)
                        {
                            // Re-throw if no error event handler could handle it
                            throw;
                        }
                    }

                    // Only move tokens that were not handled by a gateway
                    if (!((_processInstance.DefinitionsHandler.GetElementById(token.CurrentElementId)) is BpmnGateway))
                    {
                        await _processInstance.MoveToken(token);
                    }
                }
                
                // Check for tokens that are pending to merge in gateways
                var gatewayTokens = _processInstance.Tokens
                    .Where(t => t.Status == TokenStatus.PendingToMerge)
                    .ToList();
                
                if (gatewayTokens.Any())
                {
                    Console.WriteLine($"Found {gatewayTokens.Count} tokens pending for merge in gateways");
                    
                    // Group tokens by gateway to check if any can merge
                    var tokensByGateway = gatewayTokens
                        .GroupBy(t => t.CurrentElementId)
                        .ToDictionary(g => g.Key, g => g.ToList());
                    
                    // Check each gateway if it can merge and process if possible
                    foreach (var entry in tokensByGateway)
                    {
                        var gatewayId = entry.Key;
                        var tokensAtGateway = entry.Value;
                        
                        if (tokensAtGateway.Any())
                        {
                            var gateway = _processInstance.DefinitionsHandler.GetElementById(gatewayId) as BpmnGateway;
                            
                            if (gateway != null && _gatewayRouter.CanMerge(gateway, _processInstance))
                            {
                                Console.WriteLine($"Gateway {gatewayId} can merge, processing a token");
                                
                                // Just use the first token to trigger the merge
                                var token = tokensAtGateway.First();
                                var newTokens = await HandleGatewayAsync(gateway, token);
                                
                                // Add new tokens to the process instance
                                foreach (var newToken in newTokens)
                                {
                                    _processInstance.AddToken(newToken);
                                }
                            }
                        }
                    }
                }
                
                // بررسی وضعیت اجرا - اگر هیچ توکن فعالی وجود ندارد ولی توکن‌های در انتظار وجود دارند
                if (!_processInstance.Tokens.Any(t => t.Status == TokenStatus.Active) && 
                    _processInstance.Tokens.Any(t => t.Status == TokenStatus.PendingToMerge))
                {
                    // بررسی وضعیت توکن‌های در انتظار ادغام در گیت‌وی‌ها
                    Console.WriteLine("No active tokens, but pending merge tokens found. Checking gateway merge conditions...");
                    
                    // بررسی اینکه آیا شرایط ادغام در گیت‌وی تغییر کرده است یا خیر
                    // اگر بیش از ۳ دور حلقه بدون تغییر در وضعیت گیت‌وی‌ها گذشته است، اجرا را متوقف می‌کنیم
                    if (iterations > 3)
                    {
                        Console.WriteLine("Execution locked in gateway merge state. Stopping execution.");
                        break;
                    }
                }
                
                // اگر چندین دور است که هیچ توکن فعالی وجود ندارد، اجرا را متوقف می‌کنیم
                if (iterations > 5 && !activeTokens.Any())
                {
                    Console.WriteLine("Multiple iterations with no active tokens. Stopping execution.");
                    break;
                }
            }
            
            if (iterations >= maxIterations)
            {
                Console.WriteLine($"Execution stopped after reaching maximum iterations ({maxIterations}).");
                
                // یافتن توکن‌های باقی‌مانده در وضعیت‌های مختلف
                var activeTokens = _processInstance.Tokens.Where(t => t.Status == TokenStatus.Active).ToList();
                var pendingTokens = _processInstance.Tokens.Where(t => t.Status == TokenStatus.PendingToMerge).ToList();
                var waitingTokens = _processInstance.Tokens.Where(t => t.Status == TokenStatus.Waiting).ToList();
                
                Console.WriteLine($"Remaining tokens - Active: {activeTokens.Count}, Pending: {pendingTokens.Count}, Waiting: {waitingTokens.Count}");
                
                foreach (var token in activeTokens)
                {
                    Console.WriteLine($"  Active token {token.Id} at {token.CurrentElementId}");
                }
                
                foreach (var token in pendingTokens)
                {
                    Console.WriteLine($"  Pending token {token.Id} at {token.CurrentElementId}");
                }
            }
            else
            {
                Console.WriteLine($"Process execution completed after {iterations} iterations.");
            }
            
            return _processInstance;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during process execution: {ex.Message}");
            throw;
        }
        finally
        {
            // Cancel any remaining events
            foreach (var handler in _eventHandlers.Values)
            {
                foreach (var token in _processInstance.Tokens)
                {
                    await handler.CancelEvents(token.Id);
                }
            }
        }
    }

    // Complete a user task and continue the process
    public async Task<BpmnV3ProcessInstance> CompleteUserTaskAsync(Guid tokenId)
    {
        var token = _processInstance.Tokens.FirstOrDefault(t => t.Id == tokenId);
        if (token == null)
        {
            throw new InvalidOperationException($"Token with ID {tokenId} not found.");
        }

        if (token.Status != TokenStatus.Waiting)
        {
            throw new InvalidOperationException($"Token {tokenId} is not in a waiting state.");
        }

        // Reactivate the token and continue the process
        token.Reactivate();

        Console.WriteLine($"User task completed for token {token.Id}. Reactivating the token.");

        // Continue processing the reactivated token
        await StartProcessAsync();
        return _processInstance;
    }

    // Finds the first start event in the process definition
    private BpmnFlowElement FindStartEvent()
    {
        return _processInstance.DefinitionsHandler.GetStartEventsForProcess(_processInstance.ProcessElementId)
            .FirstOrDefault();
    }
    
    // جدید: دریافت نقشه اجرا شده از فرآیند
    public ProcessExecutionMap GetExecutionMap(bool includeVirtualNodesAndFlows = true)
    {
        return _processInstance.GetExecutionMap(includeVirtualNodesAndFlows);
    }
    
    // جدید: متد برای دریافت وضعیت فرآیند به صورت نقشه
    public string GetProcessStatus()
    {
        // نمایش فقط نودهای واقعی (بدون نودهای پیشمایشی)
        var map = _processInstance.GetExecutionMap(false);
        var activeNodes = map.Nodes.Where(n => n.IsActive).Select(n => n.NodeId).ToList();
        var waitingTokens = map.WaitingTokens.ToList();
        
        string status = $"فرآیند با {map.Nodes.Count} نود و {map.Flows.Count} فلو اجرا شده است.\n";
        status += $"نودهای فعال: {string.Join(", ", activeNodes)}\n";
        status += $"توکن‌های در انتظار: {waitingTokens.Count}\n";
        status += $"توکن‌های تکمیل شده: {map.CompletedTokens.Count}\n";
        status += $"توکن‌های منقضی شده: {map.ExpiredTokens.Count}\n";
        
        return status;
    }
}

// Exception class for BPMN execution errors
public class BpmnExecutionException : Exception
{
    public string ErrorCode { get; }
    
    public BpmnExecutionException(string message, string errorCode = null) : base(message)
    {
        ErrorCode = errorCode ?? "UNKNOWN_ERROR";
    }
    
    public BpmnExecutionException(string message, Exception innerException, string errorCode = null) 
        : base(message, innerException)
    {
        ErrorCode = errorCode ?? "UNKNOWN_ERROR";
    }
}