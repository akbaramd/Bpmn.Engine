using Novin.Bpmn;
using Novin.Bpmn.Core;
using Novin.Bpmn.Models;
using Novin.Bpmn.V3;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;

public class BpmnV3ProcessExecutor
{
    private readonly BpmnV3ProcessInstance _processInstance;
    private readonly ScriptHandler _scriptHandler;

    public BpmnV3ProcessExecutor(BpmnV3ProcessInstance processInstance)
    {
        _processInstance = processInstance;
        _scriptHandler = new ScriptHandler();

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
                        // Add events to the dictionary if not already added
                        if (!_processInstance.TokenEvents.ContainsKey(token.Id))
                        {
                            _processInstance.TokenEvents[token.Id] = new List<BaseEvent>();
                        }

                        foreach (var eventDefinition in boundaryEvent.Items)
                        {
                            BaseEvent bpmnEvent = eventDefinition switch
                            {
                                BpmnErrorEventDefinition errorEvent => new ErrorEvent(boundaryEvent, errorEvent, token),
                                _ => null // Add support for other event types as needed
                            };

                            if (bpmnEvent != null && !_processInstance.TokenEvents[token.Id]
                                    .Any(e => e.Event.id == bpmnEvent.Event.id))
                            {
                                _processInstance.AddEventToToken(token.Id, bpmnEvent);
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
                        await _processInstance.TriggerSpecificEvent<ErrorEvent>(token.Id);
                    }
                    catch (Exception e)
                    {
                        // مدیریت سایر خطاها
                        Console.WriteLine($"Error during token execution: {e.Message}");
                        Console.WriteLine(e.StackTrace);
                        await _processInstance.TriggerSpecificEvent<ErrorEvent>(token.Id);
                    }

                    if (_processInstance.TokenEvents.TryGetValue(token.Id, out var events) && events.Any())
                    {
                        foreach (var @event in events.Where(x=>!x.InDepended || (x.InDepended && x.IsTriggered)))
                        {
                             _processInstance.CreateToken(@event.BoundaryEvent.id);
                        }
                        
                        if (events.Any(x => x.InDepended && x.IsTriggered))
                        {
                            await _processInstance.MoveToken(token, false);
                        }
                        break;
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