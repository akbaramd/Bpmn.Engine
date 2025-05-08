using Novin.Bpmn;
using Novin.Bpmn.Core;
using Novin.Bpmn.Models;
using Novin.Bpmn.V3;
using Novin.Bpmn.V3.Events;
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

    public BpmnV3ProcessExecutor(BpmnV3ProcessInstance processInstance)
    {
        _processInstance = processInstance;
        _scriptHandler = new ScriptHandler();

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
            while (_processInstance.Tokens.Any(t => t.Status == TokenStatus.Active))
            {
                // Handle active tokens
                foreach (var token in _processInstance.Tokens.Where(t => t.Status == TokenStatus.Active).ToList())
                {
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

                    await _processInstance.MoveToken(token);
                }
            }

            Console.WriteLine("Process execution completed.");
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