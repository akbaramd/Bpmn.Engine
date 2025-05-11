using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Events;
using Novin.Bpmn.EventSourcing.Core.Models;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Novin.Bpmn.Models;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Novin.Bpmn.EventSourcing.Core.EventHandlers;

/// <summary>
/// Handles the processing of BPMN elements and tasks
/// </summary>
public class ElementProcessingHandler : BaseEventHandler<ElementProcessing>
{
    private readonly IUserTaskService _userTaskService;
    private const int MaxRetries = 3;
    private const int RetryDelay = 1000;
    
    /// <summary>
    /// ایجاد یک نمونه جدید از پردازش‌کننده رویداد فعال شدن المان
    /// </summary>
    /// <param name="logger">سیستم ثبت وقایع</param>
    /// <param name="stateStore">مخزن وضعیت</param>
    /// <param name="eventBus">گذرگاه رویداد</param>
    public ElementProcessingHandler(
        IStateStore stateStore,
        IEventStore eventStore,
        IDefinitionStore definitionStore,
        IEventBus eventBus,
        IUserTaskService userTaskService,
        ILogger<ElementProcessingHandler> logger)
        : base(stateStore, eventStore, definitionStore, logger)
    {
        _userTaskService = userTaskService ?? throw new ArgumentNullException(nameof(userTaskService));
    }
    
    /// <inheritdoc />
    protected override async Task ProcessEventAsync(ElementProcessing @event, CancellationToken cancellationToken = default)
    {
        if (@event == null)
        {
            throw new ArgumentNullException(nameof(@event));
        }

        try
        {
            Logger.LogInformation("Processing element {ElementId} of type {ElementType} in process {ProcessInstanceId}",
                @event.ElementId, @event.ElementType, @event.ProcessInstanceId);

            // Get current state with retry
            var retryCount = 0;
            BpmnProcessState state = null;
            long version = 0;

            while (retryCount < MaxRetries)
            {
                try
                {
                    state = await GetStateAsync(@event.ProcessInstanceId, cancellationToken);
                    if (state == null)
                    {
                        Logger.LogError("Process state not found for instance {ProcessInstanceId}",
                            @event.ProcessInstanceId);
                        return;
                    }
                    break;
                }
                catch (Exception)
                {
                    retryCount++;
                    if (retryCount >= MaxRetries)
                    {
                        Logger.LogError("Failed to get process state after {MaxRetries} retries for instance {ProcessInstanceId}",
                            MaxRetries, @event.ProcessInstanceId);
                        throw;
                    }
                    await Task.Delay(RetryDelay * retryCount, cancellationToken);
                }
            }

            // Get BPMN definition
            var bpmnDefinition = await DefinitionStore.GetParsedDefinitionAsync(state.DeploymentKey, cancellationToken);
            if (bpmnDefinition == null)
            {
                Logger.LogError("BPMN definition not found for process instance {ProcessInstanceId}",
                    @event.ProcessInstanceId);
                return;
            }

            // Determine handler based on element type
            var handler = DetermineElementHandler(@event.ElementType);
            if (handler != null)
            {
                await handler(@event, state, version, cancellationToken);
            }
            else
            {
                Logger.LogWarning("No handler found for element type {ElementType}", @event.ElementType);
                await CompleteElementAsync(@event, state, version, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing element {ElementId} in process {ProcessInstanceId}",
                @event.ElementId, @event.ProcessInstanceId);
            throw;
        }
    }
    
    /// <summary>
    /// Determine the appropriate handler for the element type
    /// </summary>
    private Func<ElementProcessing, BpmnProcessState, long, CancellationToken, Task> DetermineElementHandler(string elementType)
    {
        switch (elementType)
        {
            case "bpmn:UserTask": 
                return ProcessUserTaskAsync;
            case "bpmn:ServiceTask": 
                return ProcessServiceTaskAsync;
            case "bpmn:ScriptTask":
                return ProcessScriptTaskAsync;
            case "bpmn:BusinessRuleTask":
                return ProcessBusinessRuleTaskAsync;
            case "bpmn:SendTask":
                return ProcessSendTaskAsync;
            case "bpmn:ReceiveTask":
                return ProcessReceiveTaskAsync;
            case "bpmn:IntermediateCatchEvent":
                return ProcessIntermediateCatchEventAsync;
            case "bpmn:IntermediateThrowEvent":
                return ProcessIntermediateThrowEventAsync;
            case "bpmn:BoundaryEvent":
                return ProcessBoundaryEventAsync;
            case "bpmn:CallActivity":
                return ProcessCallActivityAsync;
            case "bpmn:SubProcess":
                return ProcessSubProcessAsync;
            default:
                return async (e, s, v, ct) => await CompleteElementAsync(e, s, v, ct);
        }
    }
    
    /// <summary>
    /// Evaluate a script asynchronously with all necessary context
    /// </summary>
    private async Task<object> EvaluateScriptAsync(
        string scriptContent,
        string scriptLanguage,
        ScriptContext context,
        CancellationToken cancellationToken)
    {
        // Configure script options with references to commonly needed assemblies
        var scriptOptions = ScriptOptions.Default
            .WithReferences(
                typeof(System.Linq.Enumerable).Assembly,
                typeof(System.Collections.Generic.List<>).Assembly,
                typeof(System.Console).Assembly,
                typeof(System.Collections.Generic.Dictionary<,>).Assembly,
                typeof(ScriptContext).Assembly)
            .WithImports(
                "System",
                "System.Linq", 
                "System.Collections.Generic",
                "System.Text");
                
        if (scriptLanguage == "csharp" || scriptLanguage == "c#" || scriptLanguage == "text/csharp")
        {
            // Execute the script with the context
            return await CSharpScript.EvaluateAsync(
                scriptContent, 
                scriptOptions, 
                context, 
                typeof(ScriptContext), 
                cancellationToken);
        }
        else if (scriptLanguage == "expression" || scriptLanguage == "text/expression")
        {
            // Simple expression language - just evaluate as C# expression
            return await CSharpScript.EvaluateAsync(
                scriptContent,
                scriptOptions.WithImports("System", "System.Linq", "System.Collections.Generic"),
                context,
                typeof(ScriptContext),
                cancellationToken);
        }
        else if (scriptLanguage == "javascript" || scriptLanguage == "js" || scriptLanguage == "text/javascript")
        {
            // Try to handle simple expressions via C# evaluation
            if (scriptContent.EndsWith(";"))
            {
                scriptContent = scriptContent.TrimEnd(';');
            }
            
            return await CSharpScript.EvaluateAsync(
                scriptContent,
                scriptOptions.WithImports("System", "System.Linq", "System.Collections.Generic"),
                context,
                typeof(ScriptContext),
                cancellationToken);
        }
        else
        {
            // Default to C# for any other language
            return await CSharpScript.EvaluateAsync(
                scriptContent,
                scriptOptions.WithImports("System", "System.Linq", "System.Collections.Generic"),
                context,
                typeof(ScriptContext),
                cancellationToken);
        }
    }
    
    private async Task ProcessUserTaskAsync(
        ElementProcessing @event,
        BpmnProcessState state,
        long version,
        CancellationToken cancellationToken)
    {
        try
        {
            var definition = await DefinitionStore.GetParsedDefinitionAsync(state.DeploymentKey, cancellationToken);
            if (definition == null)
            {
                throw new InvalidOperationException($"BPMN definition not found for deployment key {state.DeploymentKey}");
            }

            var userTask = FindUserTask(definition, @event.ElementId);
            if (userTask == null)
            {
                throw new InvalidOperationException($"UserTask {@event.ElementId} not found in definition");
            }

            var taskInfo = new UserTaskInfo
                {
                    TaskId = @event.ElementId,
                    ProcessInstanceId = @event.ProcessInstanceId,
                ProcessDefinitionId = state.ProcessDefinitionId,
                    ElementId = @event.ElementId,
                TaskTitle = userTask.name ?? @event.ElementId,
                TaskDescription = string.Empty,
                Assignee = userTask.assignee,
                FormKey = userTask.formId,
                Priority = 0,
                Status = UserTaskStatus.Active,
                    CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                ExecutionId = @event.ExecutionId // Track execution ID in task
            };

            if (!string.IsNullOrEmpty(userTask.candidateUsers))
            {
                taskInfo.CandidateUsers = userTask.candidateUsers.Split(',').Select(u => u.Trim()).ToList();
            }
            
            if (!string.IsNullOrEmpty(userTask.candidateGroups))
            {
                taskInfo.CandidateGroups = userTask.candidateGroups.Split(',').Select(g => g.Trim()).ToList();
            }

            await _userTaskService.CreateTaskAsync(taskInfo);
            
            Logger.LogDebug("Created user task {TaskId} in process {ProcessInstanceId}",
                @event.ElementId, @event.ProcessInstanceId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing user task {TaskId} in process {ProcessInstanceId}",
                @event.ElementId, @event.ProcessInstanceId);
            throw;
        }
    }
    
    private async Task ProcessServiceTaskAsync(
        ElementProcessing @event,
        BpmnProcessState state,
        long version,
        CancellationToken cancellationToken)
    {
        await EventBus.PublishAsync(new JobCreatedEvent
        {
            EventId = Guid.NewGuid(),
            ProcessInstanceId = @event.ProcessInstanceId,
            JobId = Guid.NewGuid().ToString(),
            ElementId = @event.ElementId,
            ElementType = @event.ElementType,
            JobType = "service-task",
            Retries = 3,
            Intent = "CREATED",
            Timestamp = DateTime.UtcNow,
            ExecutionId = @event.ExecutionId // Track execution ID in job
        }, cancellationToken);
    }
    
    private async Task ProcessScriptTaskAsync(
        ElementProcessing @event,
        BpmnProcessState state,
        long version,
        CancellationToken cancellationToken)
    {
        try
        {
            // Get the script task definition from BPMN model
            var definition = await DefinitionStore.GetParsedDefinitionAsync(state.DeploymentKey, cancellationToken);
            if (definition == null)
            {
                throw new InvalidOperationException($"BPMN definition not found for deployment key {state.DeploymentKey}");
            }

            var scriptTask = FindScriptTask(definition, @event.ElementId);
            if (scriptTask == null)
            {
                throw new InvalidOperationException($"ScriptTask {@event.ElementId} not found in definition");
            }

            // Determine script language (default to C#)
            string scriptLanguage = scriptTask.scriptFormat?.ToLowerInvariant() ?? "csharp";
            
            // Extract script content from the task and ensure it's a string
            string scriptContent = string.Empty;
            
            if (scriptTask.script != null)
            {
                // Simply convert to string, whatever the type is
                scriptContent = scriptTask.script.InnerText;
            }
            
            if (string.IsNullOrWhiteSpace(scriptContent))
            {
                Logger.LogWarning("Empty script content for task {TaskId} in process {ProcessInstanceId}", 
                    @event.ElementId, @event.ProcessInstanceId);
                
                // Even with empty script, we complete the task
                await CompleteElementAsync(@event, state, version, cancellationToken);
                return;
            }

            Logger.LogDebug("Executing {Language} script for task {TaskId} in process {ProcessInstanceId}: {ScriptContent}", 
                scriptLanguage, @event.ElementId, @event.ProcessInstanceId, scriptContent);
            
            // Create a ScriptContext object to pass process variables to the script
            var scriptContext = new ScriptContext 
            { 
                Variables = state.Variables,
                ProcessInstanceId = @event.ProcessInstanceId,
                ElementId = @event.ElementId
            };
            
            // Process the script based on the language
            object result = await EvaluateScriptAsync(scriptContent, scriptLanguage, scriptContext, cancellationToken);
            
            // Update state with any variables changed by the script
            bool variablesChanged = false;
            foreach (var variable in scriptContext.Variables)
            {
                // Check if variable value has changed
                if (variable.Value == null || 
                    !state.Variables.GetType().GetProperty(variable.Key)?.GetValue(state.Variables)?.Equals(variable.Value) == true)
                {
                    state.Variables.GetType().GetProperty(variable.Key)?.SetValue(state.Variables, variable.Value);
                    variablesChanged = true;
                }
            }
            
            // Store result in process variables
            state.Variables.result = result;
            
            if (variablesChanged)
            {
                // Save the updated state with new variables
                var retryCount = 0;
                BpmnProcessState currentState = null;
                long currentVersion = 0;

                while (retryCount < MaxRetries)
                {
                    try
                    {
                        currentState = await GetStateAsync(@event.ProcessInstanceId, cancellationToken);
                        if (currentState != null)
                        {
                            // Update variables but preserve other state properties
                            foreach (var variable in state.Variables)
                            {
                                currentState.Variables[variable.Key] = variable.Value;
                            }
                            
                            await SaveStateAsync(@event.ProcessInstanceId, currentState, currentVersion, cancellationToken);
                            break;
                        }
                    }
                    catch (Exception)
                    {
                        retryCount++;
                        if (retryCount >= MaxRetries)
                        {
                            Logger.LogError("Failed to save updated state after {MaxRetries} retries for instance {ProcessInstanceId}",
                                MaxRetries, @event.ProcessInstanceId);
                            throw;
                        }
                        await Task.Delay(RetryDelay * retryCount, cancellationToken);
                    }
                }
            }
            
            // Complete the script task
            await CompleteElementAsync(@event, state, variablesChanged ? version + 1 : version, cancellationToken);
            
            Logger.LogDebug("Script task {TaskId} executed successfully in process {ProcessInstanceId}", 
                @event.ElementId, @event.ProcessInstanceId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error executing script for task {TaskId} in process {ProcessInstanceId}: {ErrorMessage}", 
                @event.ElementId, @event.ProcessInstanceId, ex.Message);
                
            await EventBus.PublishAsync(new ElementFailed
            {
                ProcessInstanceId = @event.ProcessInstanceId,
                ElementId = @event.ElementId,
                ElementType = @event.ElementType,
                ErrorCode = "SCRIPT_ERROR",
                ErrorMessage = ex.Message,
                ExecutionId = @event.ExecutionId
            }, cancellationToken);
        }
    }
    
    private async Task ProcessBusinessRuleTaskAsync(
        ElementProcessing @event,
        BpmnProcessState state,
        long version,
        CancellationToken cancellationToken)
    {
        await EventBus.PublishAsync(new JobCreatedEvent
        {
            EventId = Guid.NewGuid(),
            ProcessInstanceId = @event.ProcessInstanceId,
            JobId = Guid.NewGuid().ToString(),
            ElementId = @event.ElementId,
            ElementType = @event.ElementType,
            JobType = "business-rule-task",
            Retries = 3,
            Intent = "CREATED",
            Timestamp = DateTime.UtcNow,
            ExecutionId = @event.ExecutionId // Track execution ID in job
        }, cancellationToken);
    }
    
    private async Task ProcessSendTaskAsync(
        ElementProcessing @event,
        BpmnProcessState state,
        long version,
        CancellationToken cancellationToken)
    {
        await EventBus.PublishAsync(new JobCreatedEvent
        {
            EventId = Guid.NewGuid(),
            ProcessInstanceId = @event.ProcessInstanceId,
            JobId = Guid.NewGuid().ToString(),
            ElementId = @event.ElementId,
            ElementType = @event.ElementType,
            JobType = "send-task",
            Retries = 3,
            Intent = "CREATED",
            Timestamp = DateTime.UtcNow,
            ExecutionId = @event.ExecutionId // Track execution ID in job
        }, cancellationToken);
    }
    
    private async Task ProcessReceiveTaskAsync(
        ElementProcessing @event,
        BpmnProcessState state,
        long version,
        CancellationToken cancellationToken)
    {
        await EventBus.PublishAsync(new MessageSubscriptionCreatedEvent
        {
            EventId = Guid.NewGuid(),
            ProcessInstanceId = @event.ProcessInstanceId,
            ElementId = @event.ElementId,
            MessageName = @event.ElementId,
            Intent = "CREATED",
            Timestamp = DateTime.UtcNow,
            ExecutionId = @event.ExecutionId // Track execution ID in subscription
        }, cancellationToken);
    }
    
    private async Task ProcessIntermediateCatchEventAsync(
        ElementProcessing @event,
        BpmnProcessState state,
        long version,
        CancellationToken cancellationToken)
    {
        await EventBus.PublishAsync(new MessageSubscriptionCreatedEvent
        {
            EventId = Guid.NewGuid(),
            ProcessInstanceId = @event.ProcessInstanceId,
            ElementId = @event.ElementId,
            MessageName = @event.ElementId,
            Intent = "CREATED",
            Timestamp = DateTime.UtcNow,
            ExecutionId = @event.ExecutionId // Track execution ID in subscription
        }, cancellationToken);
    }
    
    private async Task ProcessIntermediateThrowEventAsync(
        ElementProcessing @event,
        BpmnProcessState state,
        long version,
        CancellationToken cancellationToken)
    {
        await EventBus.PublishAsync(new JobCreatedEvent
        {
            EventId = Guid.NewGuid(),
            ProcessInstanceId = @event.ProcessInstanceId,
            JobId = Guid.NewGuid().ToString(),
            ElementId = @event.ElementId,
            ElementType = @event.ElementType,
            JobType = "throw-event",
            Retries = 3,
            Intent = "CREATED",
            Timestamp = DateTime.UtcNow,
            ExecutionId = @event.ExecutionId // Track execution ID in job
        }, cancellationToken);
    }
    
    private async Task ProcessBoundaryEventAsync(
        ElementProcessing @event,
        BpmnProcessState state,
        long version,
        CancellationToken cancellationToken)
    {
        var definition = await DefinitionStore.GetParsedDefinitionAsync(state.DeploymentKey, cancellationToken);
        if (definition == null)
        {
            throw new InvalidOperationException($"BPMN definition not found for deployment key {state.DeploymentKey}");
        }

        var boundaryEvent = FindBoundaryEvent(definition, @event.ElementId);
        if (boundaryEvent == null)
        {
            throw new InvalidOperationException($"BoundaryEvent {@event.ElementId} not found in definition");
        }

        foreach (var eventDefinition in boundaryEvent.Items ?? Array.Empty<object>())
        {
            if (eventDefinition is BpmnTimerEventDefinition timerEvent)
            {
                await ProcessTimerEventAsync(@event, timerEvent, boundaryEvent, cancellationToken);
            }
            else if (eventDefinition is BpmnMessageEventDefinition messageEvent)
            {
                await ProcessMessageEventAsync(@event, messageEvent, boundaryEvent, cancellationToken);
            }
            else if (eventDefinition is BpmnErrorEventDefinition errorEvent)
            {
                await ProcessErrorEventAsync(@event, errorEvent, boundaryEvent, cancellationToken);
            }
            else if (eventDefinition is BpmnSignalEventDefinition signalEvent)
            {
                await ProcessSignalEventAsync(@event, signalEvent, boundaryEvent, cancellationToken);
            }
        }
    }
    
    private async Task ProcessCallActivityAsync(
        ElementProcessing @event,
        BpmnProcessState state,
        long version,
        CancellationToken cancellationToken)
    {
        await EventBus.PublishAsync(new JobCreatedEvent
        {
            EventId = Guid.NewGuid(),
            ProcessInstanceId = @event.ProcessInstanceId,
            JobId = Guid.NewGuid().ToString(),
            ElementId = @event.ElementId,
            ElementType = @event.ElementType,
            JobType = "call-activity",
            Retries = 3,
            Intent = "CREATED",
            Timestamp = DateTime.UtcNow,
            ExecutionId = @event.ExecutionId // Track execution ID in job
        }, cancellationToken);
    }
    
    private async Task ProcessSubProcessAsync(
        ElementProcessing @event,
        BpmnProcessState state,
        long version,
        CancellationToken cancellationToken)
    {
        await EventBus.PublishAsync(new SubProcessStartingEvent
        {
            EventId = Guid.NewGuid(),
            ProcessInstanceId = @event.ProcessInstanceId,
            SubProcessId = @event.ElementId,
            Timestamp = DateTime.UtcNow,
            ExecutionId = @event.ExecutionId // Track execution ID in subprocess
        }, cancellationToken);
    }
    
    private async Task CompleteElementAsync(
        ElementProcessing @event,
        BpmnProcessState state,
        long version,
        CancellationToken cancellationToken)
    {
        await EventBus.PublishAsync(new ElementCompleted
        {
            ProcessInstanceId = @event.ProcessInstanceId,
            ElementId = @event.ElementId,
            ElementType = @event.ElementType,
            ExecutionId = @event.ExecutionId // Pass the execution ID to ElementCompleted
        }, cancellationToken);
    }
    
    private BpmnUserTask FindUserTask(BpmnDefinitions definitions, string taskId)
    {
        foreach (var process in definitions.Items.OfType<BpmnProcess>())
        {
            var task = process.Items?.OfType<BpmnUserTask>()
                .FirstOrDefault(t => t.id == taskId);
                
            if (task != null)
                return task;
        }
        
        return null;
    }
    
    private BpmnScriptTask FindScriptTask(BpmnDefinitions definitions, string taskId)
    {
        foreach (var process in definitions.Items.OfType<BpmnProcess>())
        {
            var task = process.Items?.OfType<BpmnScriptTask>()
                .FirstOrDefault(t => t.id == taskId);
                
            if (task != null)
                return task;
                
            // Also search in subprocesses
            if (process.Items != null)
            {
                foreach (var item in process.Items)
                {
                    if (item is BpmnSubProcess subProcess && subProcess.Items != null)
                    {
                        var subTask = subProcess.Items.OfType<BpmnScriptTask>()
                            .FirstOrDefault(t => t.id == taskId);
                            
                        if (subTask != null)
                            return subTask;
                    }
                }
            }
        }
        
        return null;
    }
    
    private BpmnBoundaryEvent FindBoundaryEvent(BpmnDefinitions definitions, string eventId)
    {
        foreach (var process in definitions.Items.OfType<BpmnProcess>())
        {
            var boundaryEvent = process.Items?.OfType<BpmnBoundaryEvent>()
                .FirstOrDefault(e => e.id == eventId);
                
            if (boundaryEvent != null)
                return boundaryEvent;
        }
        
        return null;
    }
    
    private async Task ProcessTimerEventAsync(
        ElementProcessing @event,
        BpmnTimerEventDefinition timerEvent,
        BpmnBoundaryEvent boundaryEvent,
        CancellationToken cancellationToken)
    {
        string timerType = "duration";
        string timerValue = "PT5M";
        
        if (timerEvent.TimeDuration != null)
        {
            timerType = "duration";
            timerValue = timerEvent.GetTimeDuration();
        }
        else if (timerEvent.TimeDate != null)
        {
            timerType = "date";
            timerValue = timerEvent.GetTimeDate();
        }
        else if (timerEvent.TimeCycle != null)
        {
            timerType = "cycle";
            timerValue = timerEvent.GetTimeCycle();
        }
        
        await EventBus.PublishAsync(new TimerSubscriptionCreatedEvent
        {
            EventId = Guid.NewGuid(),
            ProcessInstanceId = @event.ProcessInstanceId,
            ElementId = @event.ElementId,
            TimerId = Guid.NewGuid().ToString(),
            TimerType = timerType,
            TimerValue = timerValue,
            AttachedToElementId = boundaryEvent.attachedToRef?.Name,
            IsInterrupting = boundaryEvent.cancelActivity,
            Intent = "CREATED",
            Timestamp = DateTime.UtcNow,
            ExecutionId = @event.ExecutionId // Track execution ID in timer subscription
        }, cancellationToken);
    }
    
    private async Task ProcessMessageEventAsync(
        ElementProcessing @event,
        BpmnMessageEventDefinition messageEvent,
        BpmnBoundaryEvent boundaryEvent,
        CancellationToken cancellationToken)
    {
        string messageRef = messageEvent.messageRef?.Name ?? "unknown-message";
        
            await EventBus.PublishAsync(new MessageSubscriptionCreatedEvent
            {
                EventId = Guid.NewGuid(),
                ProcessInstanceId = @event.ProcessInstanceId,
                ElementId = @event.ElementId,
            MessageName = messageRef,
            AttachedToElementId = boundaryEvent.attachedToRef?.Name,
            IsInterrupting = boundaryEvent.cancelActivity,
                Intent = "CREATED",
            Timestamp = DateTime.UtcNow,
            ExecutionId = @event.ExecutionId // Track execution ID in message subscription
        }, cancellationToken);
    }
    
    private async Task ProcessErrorEventAsync(
        ElementProcessing @event,
        BpmnErrorEventDefinition errorEvent,
        BpmnBoundaryEvent boundaryEvent,
        CancellationToken cancellationToken)
    {
        string errorRef = errorEvent.errorRef?.Name ?? "unknown-error";
        
        await EventBus.PublishAsync(new TimerSubscriptionCreatedEvent
            {
                EventId = Guid.NewGuid(),
                ProcessInstanceId = @event.ProcessInstanceId,
                ElementId = @event.ElementId,
                TimerId = errorRef,
                TimerType = "error",
                TimerValue = errorRef,
                AttachedToElementId = boundaryEvent.attachedToRef?.Name,
                IsInterrupting = true,
                Intent = "CREATED",
                Timestamp = DateTime.UtcNow,
                ExecutionId = @event.ExecutionId // Track execution ID in error subscription
        }, cancellationToken);
    }
    
    private async Task ProcessSignalEventAsync(
        ElementProcessing @event,
        BpmnSignalEventDefinition signalEvent,
        BpmnBoundaryEvent boundaryEvent,
        CancellationToken cancellationToken)
    {
        string signalRef = signalEvent.signalRef?.Name ?? "unknown-signal";
        
            await EventBus.PublishAsync(new MessageSubscriptionCreatedEvent
            {
                EventId = Guid.NewGuid(),
                ProcessInstanceId = @event.ProcessInstanceId,
                ElementId = @event.ElementId,
            MessageName = "signal:" + signalRef,
            AttachedToElementId = boundaryEvent.attachedToRef?.Name,
            IsInterrupting = boundaryEvent.cancelActivity,
                Intent = "CREATED",
            Timestamp = DateTime.UtcNow,
            ExecutionId = @event.ExecutionId // Track execution ID in signal subscription
        }, cancellationToken);
    }
}

/// <summary>
/// Context class for passing variables to scripts
/// </summary>
public class ScriptContext
{
    /// <summary>
    /// Process variables accessible to the script
    /// </summary>
    public dynamic Variables { get; set; } = new ExpandoObject();
    
    /// <summary>
    /// Process instance ID
    /// </summary>
    public string ProcessInstanceId { get; set; }
    
    /// <summary>
    /// Element ID of the script task
    /// </summary>
    public string ElementId { get; set; }
    
    /// <summary>
    /// Sets a variable in the context
    /// </summary>
    public void SetVariable(string name, object value)
    {
        Variables[name] = value;
}

/// <summary>
    /// Evaluates an expression with the current variables
/// </summary>
    public T Evaluate<T>(string expression)
    {
        // This will use CSharpScript to evaluate the expression in the current context
        var result = CSharpScript.EvaluateAsync<T>(
            expression,
            ScriptOptions.Default
                .WithImports("System", "System.Linq", "System.Collections.Generic"),
            this).Result;
        return result;
    }
    
    /// <summary>
    /// Evaluates an expression (non-generic version)
    /// </summary>
    public object Evaluate(string expression)
    {
        var result = CSharpScript.EvaluateAsync(
            expression,
            ScriptOptions.Default
                .WithImports("System", "System.Linq", "System.Collections.Generic"),
            this).Result;
        return result;
    }
    
    /// <summary>
    /// Logs a message to the console
    /// </summary>
    public void Log(string message)
    {
        Console.WriteLine($"[Script {ElementId}] {message}");
    }
    
    /// <summary>
    /// Formats a string with variable placeholders
    /// </summary>
   
}
