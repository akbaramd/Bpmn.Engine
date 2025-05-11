using Novin.Bpmn;
using Novin.Bpmn.Contracts;
using Novin.Bpmn.Core;
using Novin.Bpmn.Models;
using Novin.Bpmn.V3;
using Novin.Bpmn.V3.Events;
using Novin.Bpmn.V3.Handlers.Gateways;
using Novin.Bpmn.V3.UserTasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;

public class BpmnProcessManager : IBpmnProcessManager
{
    private BpmnV3ProcessInstance _currentInstance;
    private readonly ScriptHandler _scriptHandler;
    private readonly Dictionary<Type, IBpmnEventHandler> _eventHandlers = new Dictionary<Type, IBpmnEventHandler>();
    private readonly BpmnV3GatewayRouter _gatewayRouter;
    private readonly IBpmnTaskManager _userTaskManager;
    private readonly IBpmnProcessInstanceAccessor _processInstanceAccessor;

    public IBpmnProcessInstanceAccessor ProcessInstanceAccessor => _processInstanceAccessor;
    
    // Property for accessing the current process instance
    public BpmnV3ProcessInstance ProcessInstance 
    { 
        get => _currentInstance; 
        set 
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value), "Process instance cannot be null");
                
            _currentInstance = value;
            
            // Initialize the instance if needed
            InitializeInstance();
        } 
    }

    public BpmnProcessManager(BpmnV3GatewayRouter gatewayRouter = null, IBpmnTaskManager userTaskManager = null, IBpmnProcessInstanceAccessor processInstanceAccessor = null)
    {
        _scriptHandler = new ScriptHandler();
        
        // Initialize gateway router
        _gatewayRouter = gatewayRouter ?? InitializeDefaultGatewayRouter();
        
        // Initialize user task manager
        _userTaskManager = userTaskManager;
        
        // Initialize process instance accessor
        _processInstanceAccessor = processInstanceAccessor;

        // Register event handlers
        RegisterEventHandlers();
    }
    
    public BpmnProcessManager(BpmnV3ProcessInstance processInstance, BpmnV3GatewayRouter gatewayRouter = null, IBpmnTaskManager userTaskManager = null, IBpmnProcessInstanceAccessor processInstanceAccessor = null)
        : this(gatewayRouter, userTaskManager, processInstanceAccessor)
    {
        ProcessInstance = processInstance;
    }
    
    // Initialize a new process instance
    private void InitializeInstance()
    {
        if (_currentInstance != null && !_currentInstance.Tokens.Any())
        {
            var startEvent = FindStartEvent();
            if (startEvent == null)
            {
                throw new InvalidOperationException("No start event found in the process definition.");
            }

            // Create the first token at the start event
            var startEventId = startEvent.id;
            _currentInstance.CreateToken(startEventId);
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
        var errorHandler = new Novin.Bpmn.V3.Events.ErrorEventHandler(null); // Will be updated when ProcessInstance is set
        _eventHandlers[typeof(BpmnErrorEventDefinition)] = errorHandler;
        
        // Register timer event handler
        var timerHandler = new TimerEventHandler(null); // Will be updated when ProcessInstance is set
        _eventHandlers[typeof(BpmnTimerEventDefinition)] = timerHandler;
        
        // Register legacy adapter for backward compatibility
        var legacyAdapter = new LegacyEventAdapter(null); // Will be updated when ProcessInstance is set
        _eventHandlers[typeof(BpmnSignalEventDefinition)] = legacyAdapter; // Support for other event types
    }

    // Update event handlers with the current process instance
    private void UpdateEventHandlers()
    {
        foreach (var handler in _eventHandlers.Values)
        {
            // Set the current process instance for all handlers
            if (handler is IBpmnEventHandler<BpmnV3ProcessInstance> typedHandler)
            {
                typedHandler.SetProcessInstance(_currentInstance);
            }
            
            // Initialize the handler
            handler.Initialize().GetAwaiter().GetResult();
        }
    }

    // Handle gateways using the dual token strategy
    private async Task<List<BpmnV3Token>> HandleGatewayAsync(BpmnGateway gateway, BpmnV3Token token)
    {
        Console.WriteLine($"Handling gateway {gateway.id} with token {token.Id}");
        return await _gatewayRouter.RouteTokenAsync(gateway, token, _currentInstance);
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
            var globals = new BpmnV3ScriptGlobals { Instance = _currentInstance };
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
    
    // جدید: متد برای مدیریت وظایف کاربری
    private async Task HandleUserTask(BpmnUserTask userTask, BpmnV3Token token)
    {
        Console.WriteLine($"Handling user task {userTask.id} for token {token.Id}");
        
        // ثبت وظیفه کاربری جدید
        await _userTaskManager.CreateTaskAssignmentAsync(token.Id, _currentInstance.Id, userTask.id, userTask.name);
        
        // تنظیم توکن به حالت انتظار
        token.SetWaiting();
        
        Console.WriteLine($"User task {userTask.id} created. Waiting for completion.");
        
        await Task.CompletedTask; // برای رعایت قرارداد async
    }

    // Start the execution process
    public async Task<BpmnV3ProcessInstance> StartProcessAsync()
    {
        if (_currentInstance == null)
        {
            throw new InvalidOperationException("No process instance has been set. Please set a process instance before starting execution.");
        }
        
        // Update event handlers with the current process instance
        UpdateEventHandlers();
        
        // Process tokens in a loop
        try
        {
            // محدودیت تعداد تکرارها برای جلوگیری از حلقه بی‌نهایت
            int maxIterations = 100; // محدودیت منطقی برای تعداد تکرارها
            int iterations = 0;

            while (_currentInstance.Tokens.Any(t => t.Status == TokenStatus.Active) && iterations < maxIterations)
            {
                iterations++;
                
                // ایجاد یک کپی از توکن‌های فعال برای جلوگیری از تغییر در حین حلقه
                var activeTokens = _currentInstance.Tokens
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

                    var attachedEvents = _currentInstance.DefinitionsHandler.GetAttachedEvents(token.CurrentElementId);

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
                        var currentElement = _currentInstance.DefinitionsHandler.GetElementById(token.CurrentElementId);
                        
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
                        else if (currentElement is BpmnUserTask userTask)
                        {
                            // جدید: اجرای منطق وظیفه کاربری
                            await HandleUserTask(userTask, token);
                            
                            // چون توکن در حالت انتظار قرار می‌گیرد، به ادامه پردازش نیازی نیست
                            continue;
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
                                _currentInstance.AddToken(newToken);
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
                    if (!((_currentInstance.DefinitionsHandler.GetElementById(token.CurrentElementId)) is BpmnGateway))
                    {
                        await _currentInstance.MoveToken(token);
                    }
                }
                
                // Check for tokens that are pending to merge in gateways
                var gatewayTokens = _currentInstance.Tokens
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
                            var gateway = _currentInstance.DefinitionsHandler.GetElementById(gatewayId) as BpmnGateway;
                            
                            if (gateway != null && _gatewayRouter.CanMerge(gateway, _currentInstance))
                            {
                                Console.WriteLine($"Gateway {gatewayId} can merge, processing a token");
                                
                                // Just use the first token to trigger the merge
                                var token = tokensAtGateway.First();
                                var newTokens = await HandleGatewayAsync(gateway, token);
                                
                                // Add new tokens to the process instance
                                foreach (var newToken in newTokens)
                                {
                                    _currentInstance.AddToken(newToken);
                                }
                            }
                        }
                    }
                }
                
                // بررسی وضعیت اجرا - اگر هیچ توکن فعالی وجود ندارد ولی توکن‌های در انتظار وجود دارند
                if (!_currentInstance.Tokens.Any(t => t.Status == TokenStatus.Active) && 
                    _currentInstance.Tokens.Any(t => t.Status == TokenStatus.PendingToMerge))
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
                var activeTokens = _currentInstance.Tokens.Where(t => t.Status == TokenStatus.Active).ToList();
                var pendingTokens = _currentInstance.Tokens.Where(t => t.Status == TokenStatus.PendingToMerge).ToList();
                var waitingTokens = _currentInstance.Tokens.Where(t => t.Status == TokenStatus.Waiting).ToList();
                
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
            
            if (_processInstanceAccessor != null)
            {
                await _processInstanceAccessor.SaveInstanceAsync(_currentInstance);
            }
            
            return _currentInstance;
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
                foreach (var token in _currentInstance.Tokens)
                {
                    await handler.CancelEvents(token.Id);
                }
            }
        }
    }

    // Implementation of the interface method - sets the instance and calls the original implementation
    public async Task<BpmnV3ProcessInstance> StartProcessAsync(BpmnV3ProcessInstance instance)
    {
        if (instance != null)
        {
            ProcessInstance = instance; // Use the property to set the instance
        }
        
        return await StartProcessAsync();
    }

    // Complete a user task and continue the process
    public async Task<BpmnV3ProcessInstance> CompleteUserTaskAsync(Guid tokenId, string userId, Dictionary<string, object> formData = null)
    {
        if (_currentInstance == null)
        {
            throw new InvalidOperationException("No process instance has been set.");
        }
        
        var token = _currentInstance.Tokens.FirstOrDefault(t => t.Id == tokenId);
        if (token == null)
        {
            throw new InvalidOperationException($"Token with ID {tokenId} not found.");
        }

        if (token.Status != TokenStatus.Waiting)
        {
            throw new InvalidOperationException($"Token {tokenId} is not in a waiting state.");
        }
        
        // تکمیل وظیفه کاربری و ذخیره داده‌های فرم
        var completed = await _userTaskManager.CompleteTaskAssignmentAsync(tokenId, userId, formData);
        
        // اضافه کردن داده‌های فرم به متغیرهای فرآیند
        if (formData != null)
        {
            foreach (var entry in formData)
            {
                _currentInstance.SetVariable(entry.Key, entry.Value);
            }
        }

        // Reactivate the token and continue the process
        token.Reactivate();

        Console.WriteLine($"User task completed for token {token.Id} by user {userId}. Reactivating the token.");

        // Continue processing the reactivated token
        await StartProcessAsync();
        return _currentInstance;
    }
    
    // جدید: متد برای گرفتن وظایف کاربری یک کاربر خاص
    public async Task<List<BpmnV3UserTaskAssignment>> GetUserTasksForUser(string userId, List<string> userGroups = null)
    {
        return await _userTaskManager.GetAvailableTasksForUserAsync(userId, userGroups);
    }
    
    // جدید: متد برای گرفتن وظایف کاربری فرآیند
    public async Task<List<BpmnV3UserTaskAssignment>> GetAllUserTasks()
    {
        if (_currentInstance == null)
        {
            throw new InvalidOperationException("No process instance has been set.");
        }
        
        return await _userTaskManager.GetTaskAssignmentsForProcessInstanceAsync(_currentInstance.Id);
    }
    
    // جدید: متد برای تخصیص وظیفه به کاربر
    public async Task<BpmnV3UserTaskAssignment> ClaimUserTask(Guid tokenId, string userId)
    {
        await _userTaskManager.ClaimTaskAssignmentAsync(tokenId, userId);
        return await _userTaskManager.GetTaskAssignmentAsync(tokenId);
    }

    // Finds the first start event in the process definition
    private BpmnFlowElement FindStartEvent()
    {
        if (_currentInstance == null)
        {
            throw new InvalidOperationException("No process instance has been set.");
        }
        
        return _currentInstance.DefinitionsHandler.GetStartEventsForProcess(_currentInstance.ProcessElementId)
            .FirstOrDefault();
    }
    
    // جدید: دریافت نقشه اجرا شده از فرآیند
    public ProcessExecutionMap GetExecutionMap(bool includeVirtualNodesAndFlows = true)
    {
        if (_currentInstance == null)
        {
            throw new InvalidOperationException("No process instance has been set.");
        }
        
        return _currentInstance.GetExecutionMap(includeVirtualNodesAndFlows);
    }
    
    // جدید: متد برای دریافت وضعیت فرآیند به صورت نقشه
    public string GetProcessStatus()
    {
        if (_currentInstance == null)
        {
            throw new InvalidOperationException("No process instance has been set.");
        }
        
        // نمایش فقط نودهای واقعی (بدون نودهای پیشمایشی)
        var map = _currentInstance.GetExecutionMap(false);
        var activeNodes = map.Nodes.Where(n => n.IsActive).Select(n => n.NodeId).ToList();
        var waitingTokens = map.WaitingTokens.ToList();
        
        string status = $"فرآیند با {map.Nodes.Count} نود و {map.Flows.Count} فلو اجرا شده است.\n";
        status += $"نودهای فعال: {string.Join(", ", activeNodes)}\n";
        status += $"توکن‌های در انتظار: {waitingTokens.Count}\n";
        status += $"توکن‌های تکمیل شده: {map.CompletedTokens.Count}\n";
        status += $"توکن‌های منقضی شده: {map.ExpiredTokens.Count}\n";
        
        // جدید: اضافه کردن اطلاعات وظایف کاربری
        var userTasksTask = _userTaskManager.GetTaskAssignmentsForProcessInstanceAsync(_currentInstance.Id);
        userTasksTask.Wait(); // Synchronously wait for task to complete
        var userTasks = userTasksTask.Result;
        
        status += $"وظایف کاربری فعال: {userTasks.Count}\n";
        if (userTasks.Any())
        {
            status += "لیست وظایف کاربری:\n";
            foreach (var task in userTasks)
            {
                status += $"- {task.TaskName} (توکن: {task.TokenId})";
                if (!string.IsNullOrEmpty(task.Assignee))
                {
                    status += $", تخصیص یافته به: {task.Assignee}";
                }
                else if (task.CandidateUsers.Any())
                {
                    status += $", کاندیداها: {string.Join(", ", task.CandidateUsers)}";
                }
                status += "\n";
            }
        }
        
        return status;
    }

    // Implementation of the interface methods
    public async Task<BpmnV3ProcessInstance> CreateProcessInstanceAsync(string deploymentKey, string processId, string definitionXml, Dictionary<string, object> variables = null)
    {
        // Create a new process instance with the given parameters
        var instance = new BpmnV3ProcessInstance(processId, definitionXml)
        {
            DeploymentKey = deploymentKey
        };
        
        // Set initial variables if provided
        if (variables != null)
        {
            foreach (var entry in variables)
            {
                instance.SetVariable(entry.Key, entry.Value);
            }
        }
        
        // Save the new instance
        if (_processInstanceAccessor != null)
        {
            await _processInstanceAccessor.SaveInstanceAsync(instance);
        }
        
        return instance;
    }

    public async Task<BpmnV3ProcessInstance> ResumeProcessAsync(string instanceId)
    {
        if (_processInstanceAccessor == null)
        {
            throw new InvalidOperationException("Process instance accessor is not available.");
        }
        
        // Load the process instance
        var instance = await _processInstanceAccessor.GetInstanceAsync(instanceId);
        if (instance == null)
        {
            throw new InvalidOperationException($"Process instance with ID {instanceId} not found.");
        }
        
        // Set the current instance
        ProcessInstance = instance;
            
        // Continue processing
        return await StartProcessAsync();
    }

    public async Task<bool> UpdateProcessStatusAsync(string instanceId, ProcessInstanceStatus status)
    {
        if (_processInstanceAccessor == null)
        {
            throw new InvalidOperationException("Process instance accessor is not available.");
        }
        
        return await _processInstanceAccessor.UpdateInstanceStatusAsync(instanceId, status);
    }

    public async Task<BpmnV3ProcessInstance> GetProcessInstanceAsync(string instanceId)
    {
        if (_processInstanceAccessor == null)
        {
            throw new InvalidOperationException("Process instance accessor is not available.");
        }
        
        return await _processInstanceAccessor.GetInstanceAsync(instanceId);
    }

    public async Task<BpmnV3ProcessInstance> GetProcessInstanceByTokenAsync(Guid tokenId)
    {
        if (_processInstanceAccessor == null)
        {
            throw new InvalidOperationException("Process instance accessor is not available.");
        }
        
        return await _processInstanceAccessor.GetInstanceByTokenAsync(tokenId);
    }

    public async Task<IEnumerable<BpmnV3ProcessInstance>> GetAllActiveInstancesAsync()
    {
        if (_processInstanceAccessor == null)
        {
            throw new InvalidOperationException("Process instance accessor is not available.");
        }
        
        return await _processInstanceAccessor.GetAllActiveInstancesAsync();
    }

    public async Task<IEnumerable<BpmnV3ProcessInstance>> GetInstancesByDeploymentKeyAsync(string deploymentKey)
    {
        if (_processInstanceAccessor == null)
        {
            throw new InvalidOperationException("Process instance accessor is not available.");
        }
        
        return await _processInstanceAccessor.GetInstancesByDeploymentKeyAsync(deploymentKey);
    }

    public async Task<bool> SaveProcessInstanceAsync(BpmnV3ProcessInstance instance)
    {
        if (_processInstanceAccessor == null)
        {
            throw new InvalidOperationException("Process instance accessor is not available.");
        }
        
        // If this is our current instance, update it
        if (instance != null && _currentInstance != null && instance.Id == _currentInstance.Id)
        {
            _currentInstance = instance;
        }
        
        await _processInstanceAccessor.SaveInstanceAsync(instance);
        return true;
    }

    public async Task<bool> TerminateProcessInstanceAsync(string instanceId)
    {
        if (_processInstanceAccessor == null)
        {
            throw new InvalidOperationException("Process instance accessor is not available.");
        }
        
        // Update the process status to Terminated
        return await _processInstanceAccessor.UpdateInstanceStatusAsync(instanceId, ProcessInstanceStatus.Terminated);
    }

    public async Task<bool> DeleteProcessInstanceAsync(string instanceId)
    {
        if (_processInstanceAccessor == null)
        {
            throw new InvalidOperationException("Process instance accessor is not available.");
        }
        
        // If this is our current instance, clear it
        if (_currentInstance != null && _currentInstance.Id == instanceId)
        {
            _currentInstance = null;
        }
        
        return await _processInstanceAccessor.DeleteInstanceAsync(instanceId);
    }

    public ProcessInstanceDetails CreateProcessInstanceDetails(BpmnV3ProcessInstance instance)
    {
        if (instance == null)
        {
            throw new ArgumentNullException(nameof(instance));
        }
        
        // Create and populate the details object
        var details = new ProcessInstanceDetails
        {
            Id = instance.Id,
            ProcessId = instance.ProcessElementId,
            DeploymentKey = instance.DeploymentKey,
            Status = DetermineInstanceStatus(instance),
            Variables = instance.Variables
        };
        
        // Count token states
        details.ActiveTokens = instance.Tokens.Where(t => t.Status == TokenStatus.Active).ToList();
        details.WaitingTokens = instance.Tokens.Where(t => t.Status == TokenStatus.Waiting).ToList();
        
        // Set timestamps if available
        details.StartedAt = DateTime.Now;
        
        return details;
    }
    
    // Helper method to determine the status of a process instance
    private ProcessInstanceStatus DetermineInstanceStatus(BpmnV3ProcessInstance instance)
    {
        if (instance.Tokens.Any(t => t.Status == TokenStatus.Active))
        {
            return ProcessInstanceStatus.Active;
        }
        else if (instance.Tokens.Any(t => t.Status == TokenStatus.Waiting))
        {
            return ProcessInstanceStatus.Active; // Still considered active if waiting
        }
        else if (instance.Tokens.All(t => t.Status == TokenStatus.Completed))
        {
            return ProcessInstanceStatus.Completed;
        }
        else if (instance.Variables.error != null)
        {
            return ProcessInstanceStatus.Error;
        }
      
        // Default
        return ProcessInstanceStatus.Active;
    }

    public async Task<BpmnProcessManager> GetExecutorForInstanceAsync(string instanceId)
    {
        var instance = await GetProcessInstanceAsync(instanceId);
        if (instance == null)
        {
            throw new InvalidOperationException($"Process instance with ID {instanceId} not found.");
        }
        
        return new BpmnProcessManager(instance, _gatewayRouter, _userTaskManager, _processInstanceAccessor);
    }

    public async Task<BpmnProcessManager> GetExecutorForTokenAsync(Guid tokenId)
    {
        var instance = await GetProcessInstanceByTokenAsync(tokenId);
        if (instance == null)
        {
            throw new InvalidOperationException($"Process instance for token {tokenId} not found.");
        }
        
        return new BpmnProcessManager(instance, _gatewayRouter, _userTaskManager, _processInstanceAccessor);
    }
}

// Event handler interface with process instance update capability
public interface IBpmnEventHandler<T>
{
    void SetProcessInstance(T instance);
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