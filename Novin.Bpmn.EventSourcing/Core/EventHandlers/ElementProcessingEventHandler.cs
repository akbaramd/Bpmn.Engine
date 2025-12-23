using Novin.Bpmn.EventSourcing.Core.Executions;
using Novin.Bpmn.EventSourcing.Events;
using System;
using System.Threading;
using System.Threading.Tasks;
using ExecutionContext = Novin.Bpmn.EventSourcing.Core.Executions.ExecutionContext;

public class ElementProcessingEventHandler : BpmnEventHandlerBase<ElementProcessing>
{
    private readonly IExecutionContextRepository _contextRepository;

    public ElementProcessingEventHandler(IServiceProvider serviceProvider,
                                         IExecutionContextRepository contextRepository)
        : base(serviceProvider)
    {
        _contextRepository = contextRepository ?? throw new ArgumentNullException(nameof(contextRepository));
    }

    public override async Task HandleAsync(ElementProcessing @event, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[ElementProcessingHandler] Received event: {@event.EventType}, ElementId: {@event.ElementId}, ElementType: {@event.ElementType}, ExecutionId: {@event.ExecutionId}");
        
        var context = _contextRepository.Get(@event.ExecutionId);
        if (context == null)
        {
            Console.WriteLine($"[ElementProcessingHandler] ERROR: ExecutionContext not found for Id {@event.ExecutionId}");
            throw new InvalidOperationException($"ExecutionContext not found for Id {@event.ExecutionId}");
        }

        Console.WriteLine($"[ElementProcessingHandler] Event type: {@event.GetType().Name}, Is ScriptTaskProcessing: {@event is ScriptTaskProcessing}");
        
        if (@event is ScriptTaskProcessing scriptTaskEvent)
        {
            Console.WriteLine($"[ElementProcessingHandler] ScriptTaskProcessing detected! Script: '{scriptTaskEvent.Script}', ScriptFormat: {scriptTaskEvent.ScriptFormat}");
        }

        switch (@event)
        {
            case UserTaskProcessing userTask:
                Console.WriteLine($"[ElementProcessingHandler] Routing to HandleUserTaskProcessingAsync");
                await HandleUserTaskProcessingAsync(userTask, context);
                break;

            case ServiceTaskProcessing serviceTask:
                Console.WriteLine($"[ElementProcessingHandler] Routing to HandleServiceTaskProcessingAsync");
                await HandleServiceTaskProcessingAsync(serviceTask, context);
                break;

            case ScriptTaskProcessing scriptTask:
                Console.WriteLine($"[ElementProcessingHandler] Routing to HandleScriptTaskProcessingAsync");
                await HandleScriptTaskProcessingAsync(scriptTask, context);
                break;

            case BusinessRuleTaskProcessing businessRuleTask:
                Console.WriteLine($"[ElementProcessingHandler] Routing to HandleBusinessRuleTaskProcessingAsync");
                await HandleBusinessRuleTaskProcessingAsync(businessRuleTask, context);
                break;

            default:
                Console.WriteLine($"[ElementProcessingHandler] Routing to HandleDefaultProcessingAsync (event type: {@event.GetType().Name})");
                await HandleDefaultProcessingAsync(@event, context);
                break;
        }
        
        Console.WriteLine($"[ElementProcessingHandler] Handler completed for ElementId: {@event.ElementId}");
    }

    private async Task HandleUserTaskProcessingAsync(UserTaskProcessing evt, ExecutionContext context)
    {
        UpdateContextAndPublishCompleted(evt, context);
        await Task.CompletedTask;
    }

    private async Task HandleServiceTaskProcessingAsync(ServiceTaskProcessing evt, ExecutionContext context)
    {
        UpdateContextAndPublishCompleted(evt, context);
        await Task.CompletedTask;
    }

    private async Task HandleScriptTaskProcessingAsync(ScriptTaskProcessing evt, ExecutionContext context)
    {
        Console.WriteLine($"[ScriptTask] Executing script for ElementId: {evt.ElementId}, Script: '{evt.Script}', ScriptFormat: {evt.ScriptFormat}");
        
        // Execute script and set variables
        var setVariables = ExecuteScript(evt.Script, evt.ScriptFormat, context);
        
        Console.WriteLine($"[ScriptTask] Script execution completed. Variables set: {string.Join(", ", setVariables.Select(kv => $"{kv.Key}={kv.Value}"))}");
        Console.WriteLine($"[ScriptTask] ExecutionContext LocalVariables after script: {string.Join(", ", context.LocalVariables.Select(kv => $"{kv.Key}={kv.Value}"))}");
        
        // Publish VariablesSet event for event sourcing with Process scope
        // Script tasks typically set process-level variables that should be available throughout the process
        if (setVariables.Count > 0)
        {
            var variablesSetEvent = new VariablesSet
            {
                EventId = Guid.NewGuid(),
                InstanceId = context.InstanceId,
                DeploymentId = evt.DeploymentId,
                DeploymentKey = evt.DeploymentKey,
                ProcessId = evt.ProcessId,
                ExecutionId = context.ContextId,
                Variables = setVariables,
                Scope = VariableScope.Process,
                Timestamp = DateTime.UtcNow
            };
            
            Console.WriteLine($"[ScriptTask] Publishing VariablesSet event (EventId: {variablesSetEvent.EventId}, Scope: {variablesSetEvent.Scope}) with variables: {string.Join(", ", setVariables.Select(kv => $"{kv.Key}={kv.Value}"))}");
            AppendEvent(variablesSetEvent);
        }
        else
        {
            Console.WriteLine($"[ScriptTask] WARNING: No variables were set by script execution!");
        }
        
        UpdateContextAndPublishCompleted(evt, context);
        await Task.CompletedTask;
    }

    private Dictionary<string, object?> ExecuteScript(string script, string? scriptFormat, ExecutionContext context)
    {
        var setVariables = new Dictionary<string, object?>();
        
        if (string.IsNullOrWhiteSpace(script))
        {
            Console.WriteLine($"[ExecuteScript] WARNING: Script is null or empty!");
            return setVariables;
        }

        Console.WriteLine($"[ExecuteScript] Parsing script: '{script}'");
        
        // Simple script execution for Java/JavaScript-like syntax
        // Parse simple assignment statements like "variable = value;"
        var scriptLines = script.Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        
        Console.WriteLine($"[ExecuteScript] Found {scriptLines.Length} script lines to process");
        
        foreach (var line in scriptLines)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmedLine))
                continue;

            Console.WriteLine($"[ExecuteScript] Processing line: '{trimmedLine}'");

            // Parse assignment: "variable = value"
            var assignmentIndex = trimmedLine.IndexOf('=');
            if (assignmentIndex > 0)
            {
                var variableName = trimmedLine.Substring(0, assignmentIndex).Trim();
                var valueExpression = trimmedLine.Substring(assignmentIndex + 1).Trim();

                Console.WriteLine($"[ExecuteScript] Parsed assignment: variable='{variableName}', expression='{valueExpression}'");

                // Evaluate value expression
                object? value = EvaluateScriptExpression(valueExpression, context.LocalVariables);
                
                Console.WriteLine($"[ExecuteScript] Evaluated value: {value} (type: {value?.GetType().Name ?? "null"})");
                
                // Set variable in context
                context.LocalVariables[variableName] = value;
                setVariables[variableName] = value;
                
                Console.WriteLine($"[ExecuteScript] Set variable '{variableName}' = {value} in ExecutionContext");
            }
            else
            {
                Console.WriteLine($"[ExecuteScript] WARNING: Line '{trimmedLine}' does not contain assignment (no '=' found)");
            }
        }
        
        Console.WriteLine($"[ExecuteScript] Script execution complete. Total variables set: {setVariables.Count}");
        return setVariables;
    }

    private object? EvaluateScriptExpression(string expression, Dictionary<string, object?> variables)
    {
        var trimmed = expression.Trim();
        
        // Boolean literals
        if (trimmed.Equals("true", StringComparison.OrdinalIgnoreCase))
            return true;
        if (trimmed.Equals("false", StringComparison.OrdinalIgnoreCase))
            return false;
        
        // Numeric literals
        if (int.TryParse(trimmed, out var intValue))
            return intValue;
        if (double.TryParse(trimmed, out var doubleValue))
            return doubleValue;
        
        // String literals (with or without quotes)
        if (trimmed.StartsWith("\"") && trimmed.EndsWith("\""))
            return trimmed.Substring(1, trimmed.Length - 2);
        if (trimmed.StartsWith("'") && trimmed.EndsWith("'"))
            return trimmed.Substring(1, trimmed.Length - 2);
        
        // Variable reference
        if (variables.TryGetValue(trimmed, out var varValue))
            return varValue;
        
        // Default: return as string
        return trimmed;
    }

    private async Task HandleBusinessRuleTaskProcessingAsync(BusinessRuleTaskProcessing evt, ExecutionContext context)
    {
        UpdateContextAndPublishCompleted(evt, context);
        await Task.CompletedTask;
    }

    private async Task HandleDefaultProcessingAsync(ElementProcessing evt, ExecutionContext context)
    {
        UpdateContextAndPublishCompleted(evt, context);
        await Task.CompletedTask;
    }

    private void UpdateContextAndPublishCompleted(ElementProcessing evt, ExecutionContext context)
    {
        context.State = ExecutionState.Active;
        _contextRepository.Save(context);

        var completedEvent = new ElementCompleted
        {
            EventId = Guid.NewGuid(),
            InstanceId = context.InstanceId,
            DeploymentId = evt.DeploymentId,
            DeploymentKey = evt.DeploymentKey,
            ProcessId = evt.ProcessId,
            ElementId = evt.ElementId,
            ExecutionId = context.ContextId,
            Timestamp = DateTime.UtcNow,
            ElementType = evt.ElementType,
            Version = context.Version,
        };

        AppendEvent(completedEvent);
    }
}
