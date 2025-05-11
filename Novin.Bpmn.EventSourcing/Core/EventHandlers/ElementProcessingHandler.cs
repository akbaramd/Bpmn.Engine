using Microsoft.Extensions.Logging;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Events;
using Novin.Bpmn.EventSourcing.Core.Models;
using System;
using System.Collections.Generic;
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
public class ElementProcessingHandler : IBpmnEventHandler<ElementProcessing>
{
    private readonly ILogger<ElementProcessingHandler> _logger;
    private readonly IStateStore _stateStore;
    private readonly IBpmnDefinitionStorage _bpmnDefinitionStorage;
    private readonly IEventBus _eventBus;
    private readonly IUserTaskService _userTaskService;
    
    /// <summary>
    /// ایجاد یک نمونه جدید از پردازش‌کننده رویداد فعال شدن المان
    /// </summary>
    /// <param name="logger">سیستم ثبت وقایع</param>
    /// <param name="stateStore">مخزن وضعیت</param>
    /// <param name="eventBus">گذرگاه رویداد</param>
    public ElementProcessingHandler(
        ILogger<ElementProcessingHandler> logger,
        IStateStore stateStore,
        IEventBus eventBus, 
        IBpmnDefinitionStorage bpmnDefinitionStorage, 
        IUserTaskService userTaskService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _bpmnDefinitionStorage = bpmnDefinitionStorage ?? throw new ArgumentNullException(nameof(bpmnDefinitionStorage));
        _userTaskService = userTaskService;
    }
    
    /// <inheritdoc />
    public async Task HandleAsync(ElementProcessing @event, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Processing element {ElementId} of type {ElementType} in process {ProcessInstanceId}", 
            @event.ElementId, @event.ElementType, @event.ProcessInstanceId);
        
        // Get state asynchronously while preparing for processing
        var stateTask = _stateStore.GetStateWithVersionAsync<BpmnProcessState>(@event.ProcessInstanceId);
        
        // While fetching state, prepare for element-specific processing
        var elementHandler = DetermineElementHandler(@event.ElementType);
        
        // Wait for state
        var (state, version) = await stateTask;
        
        if (state == null)
        {
            _logger.LogWarning("Process state not found for {ProcessInstanceId}", @event.ProcessInstanceId);
            return;
        }

        try
        {
            // Track event in execution path if execution ID is provided
            if (!string.IsNullOrEmpty(@event.ExecutionId))
            {
                state.AddEventToExecution(@event.ExecutionId, @event);
                
                // Save state with the added event
                await _stateStore.SaveStateAsync(@event.ProcessInstanceId, state, version);
                version++; // Increment version after save
            }
            
            // Dispatch to appropriate element handler
            await elementHandler(@event, state, version, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing element {ElementId} in process {ProcessInstanceId}",
                @event.ElementId, @event.ProcessInstanceId);
                
            await _eventBus.PublishAsync(new ElementFailed
            {
                ProcessInstanceId = @event.ProcessInstanceId,
                ElementId = @event.ElementId,
                ElementType = @event.ElementType,
                ErrorCode = "PROCESSING_ERROR",
                ErrorMessage = ex.Message,
                ExecutionId = @event.ExecutionId // Pass execution ID to failure event
            }, cancellationToken);
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
            var definition = _bpmnDefinitionStorage.GetParsedDefinition(state.DeploymentKey);
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
            
            _logger.LogDebug("Created user task {TaskId} in process {ProcessInstanceId}",
                @event.ElementId, @event.ProcessInstanceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing user task {TaskId} in process {ProcessInstanceId}",
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
        await _eventBus.PublishAsync(new JobCreatedEvent
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
            var definition = _bpmnDefinitionStorage.GetParsedDefinition(state.DeploymentKey);
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
                
                // If it looks like XML, try to extract just the text content
            }
            
            if (string.IsNullOrWhiteSpace(scriptContent))
            {
                _logger.LogWarning("Empty script content for task {TaskId} in process {ProcessInstanceId}", 
                    @event.ElementId, @event.ProcessInstanceId);
                
                // Even with empty script, we complete the task
                await CompleteElementAsync(@event, state, version, cancellationToken);
                return;
            }
            
            _logger.LogDebug("Executing {Language} script for task {TaskId} in process {ProcessInstanceId}: {ScriptContent}", 
                scriptLanguage, @event.ElementId, @event.ProcessInstanceId, scriptContent);
            
            // Create a ScriptContext object to pass process variables to the script
            var scriptContext = new ScriptContext 
            { 
                Variables = state.Variables,
                ProcessInstanceId = @event.ProcessInstanceId,
                ElementId = @event.ElementId
            };
            
            // Process the script based on the language
            object result = null;
            
            if (scriptLanguage == "csharp" || scriptLanguage == "c#" || scriptLanguage == "text/csharp")
            {
                // Configure script options with references to commonly needed assemblies
                var scriptOptions = ScriptOptions.Default
                    .WithReferences(
                        typeof(System.Linq.Enumerable).Assembly,
                        typeof(System.Collections.Generic.List<>).Assembly,
                        typeof(System.Console).Assembly,
                        typeof(ScriptContext).Assembly)
                    .WithImports(
                        "System",
                        "System.Linq", 
                        "System.Collections.Generic",
                        "System.Text");
                
                // Execute the script with the context
                result = await CSharpScript.EvaluateAsync(
                    scriptContent, 
                    scriptOptions, 
                    scriptContext, 
                    typeof(ScriptContext), 
                    cancellationToken);
            }
            else if (scriptLanguage == "expression" || scriptLanguage == "text/expression")
            {
                // Simple expression language - just evaluate as C# expression
                result = await CSharpScript.EvaluateAsync(
                    scriptContent,
                    ScriptOptions.Default
                        .WithImports("System", "System.Linq", "System.Collections.Generic"),
                    scriptContext,
                    typeof(ScriptContext),
                    cancellationToken);
            }
            else if (scriptLanguage == "javascript" || scriptLanguage == "js" || scriptLanguage == "text/javascript")
            {
                // Not directly supported - log a warning
                _logger.LogWarning("JavaScript scripts are not natively supported. Task: {TaskId}", @event.ElementId);
                
                // Try to handle simple expressions via C# evaluation
                if (scriptContent.EndsWith(";"))
                {
                    scriptContent = scriptContent.TrimEnd(';');
                }
                
                try
                {
                    result = await CSharpScript.EvaluateAsync(
                        scriptContent,
                        ScriptOptions.Default
                            .WithImports("System", "System.Linq", "System.Collections.Generic"),
                        scriptContext,
                        typeof(ScriptContext),
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Failed to evaluate JavaScript as C# expression: {Error}", ex.Message);
                }
            }
            else
            {
                _logger.LogWarning("Unsupported script language: {Language} for task {TaskId}. Using as C# script.",
                    scriptLanguage, @event.ElementId);
                
                // Try as C# anyway
                result = await CSharpScript.EvaluateAsync(
                    scriptContent,
                    ScriptOptions.Default
                        .WithImports("System", "System.Linq", "System.Collections.Generic"),
                    scriptContext,
                    typeof(ScriptContext),
                    cancellationToken);
            }
            
            // Update state with any variables changed by the script
            bool variablesChanged = false;
            foreach (var variable in scriptContext.Variables)
            {
                // Check if variable is new or changed
                if (!state.Variables.ContainsKey(variable.Key) || 
                    !object.Equals(state.Variables[variable.Key], variable.Value))
                {
                    state.Variables[variable.Key] = variable.Value;
                    variablesChanged = true;
                }
            }
            
            // Add any output result from the script to variables if present
            if (result != null && !scriptContext.Variables.ContainsKey("result"))
            {
                state.Variables["result"] = result;
                variablesChanged = true;
            }
            
            if (variablesChanged)
            {
                // Save the updated state with new variables
                var (currentState, currentVersion) = await _stateStore.GetStateWithVersionAsync<BpmnProcessState>(
                    @event.ProcessInstanceId, cancellationToken);
                
                if (currentState != null)
                {
                    // Update variables but preserve other state properties
                    foreach (var variable in state.Variables)
                    {
                        currentState.Variables[variable.Key] = variable.Value;
                    }
                    
                    await _stateStore.SaveStateAsync(
                        @event.ProcessInstanceId, currentState, currentVersion, cancellationToken);
                }
            }
            
            // Complete the script task
            await CompleteElementAsync(@event, state, variablesChanged ? version + 1 : version, cancellationToken);
            
            _logger.LogDebug("Script task {TaskId} executed successfully in process {ProcessInstanceId}", 
                @event.ElementId, @event.ProcessInstanceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing script for task {TaskId} in process {ProcessInstanceId}: {ErrorMessage}", 
                @event.ElementId, @event.ProcessInstanceId, ex.Message);
                
            await _eventBus.PublishAsync(new ElementFailed
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
        await _eventBus.PublishAsync(new JobCreatedEvent
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
        await _eventBus.PublishAsync(new JobCreatedEvent
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
        await _eventBus.PublishAsync(new MessageSubscriptionCreatedEvent
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
        await _eventBus.PublishAsync(new MessageSubscriptionCreatedEvent
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
        await _eventBus.PublishAsync(new JobCreatedEvent
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
        var definition = _bpmnDefinitionStorage.GetParsedDefinition(state.DeploymentKey);
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
        await _eventBus.PublishAsync(new JobCreatedEvent
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
        await _eventBus.PublishAsync(new SubProcessStartingEvent
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
        await _eventBus.PublishAsync(new ElementCompleted
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
        
        await _eventBus.PublishAsync(new TimerSubscriptionCreatedEvent
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
        
        await _eventBus.PublishAsync(new MessageSubscriptionCreatedEvent
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
        
        await _eventBus.PublishAsync(new TimerSubscriptionCreatedEvent
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
        
        await _eventBus.PublishAsync(new MessageSubscriptionCreatedEvent
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
    public Dictionary<string, object> Variables { get; set; } = new Dictionary<string, object>();
    
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
    /// Gets a variable from the context
    /// </summary>
    public T GetVariable<T>(string name, T defaultValue = default)
    {
        if (Variables.TryGetValue(name, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return defaultValue;
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
    public string Format(string template)
    {
        // Replace ${varName} with variable values
        return Regex.Replace(template, @"\$\{([^}]+)\}", match =>
        {
            var varName = match.Groups[1].Value;
            if (Variables.TryGetValue(varName, out var value))
            {
                return value?.ToString() ?? "";
            }
            return match.Value; // Keep original if not found
        });
    }
}
