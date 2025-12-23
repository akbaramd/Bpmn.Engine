using Novin.Bpmn.EventSourcing.Core.Executions;
using Novin.Bpmn.EventSourcing.Core.Topology;
using Novin.Bpmn.EventSourcing.Core.Process;
using Novin.Bpmn.EventSourcing.Core.Services;
using Novin.Bpmn.EventSourcing.Events;
using Novin.Bpmn.EventSourcing.Feel;
using Novin.Bpmn.Models.Models;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ExecutionContext = Novin.Bpmn.EventSourcing.Core.Executions.ExecutionContext;
using ScriptGlobals = Novin.Bpmn.EventSourcing.Core.ScriptGlobals;

public class ElementProcessingEventHandler : BpmnEventHandlerBase<ElementProcessing>
{
    private readonly IExecutionContextRepository _contextRepository;
    private readonly IFlowTopologyStore _topologyStore;
    private readonly IProcessStateStore _processStateStore;
    private readonly IoMappingApplier _ioMappingApplier;

    public ElementProcessingEventHandler(IServiceProvider serviceProvider,
                                         IExecutionContextRepository contextRepository,
                                         IFlowTopologyStore topologyStore,
                                         IProcessStateStore processStateStore)
        : base(serviceProvider)
    {
        _contextRepository = contextRepository ?? throw new ArgumentNullException(nameof(contextRepository));
        _topologyStore = topologyStore ?? throw new ArgumentNullException(nameof(topologyStore));
        _processStateStore = processStateStore ?? throw new ArgumentNullException(nameof(processStateStore));
        _ioMappingApplier = new IoMappingApplier();
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
        
        // Apply input mappings: Process Variables → Node Variables (before script execution)
        ApplyInputMappings(evt, context);
        
        // Execute C# script using ScriptHandler
        var setVariables = await ExecuteCSharpScriptAsync(evt.Script, evt.ScriptFormat, context);
        
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

    /// <summary>
    /// Applies input mappings from process variables to node variables before script execution.
    /// </summary>
    private void ApplyInputMappings(ElementProcessing evt, ExecutionContext context)
    {
        try
        {
            var topology = _topologyStore.Get(evt.DeploymentId, evt.ProcessId);
            if (topology == null || !topology.Nodes.TryGetValue(evt.ElementId, out var node))
            {
                Console.WriteLine($"[IoMapping] Topology or node not found for ElementId: {evt.ElementId}");
                return;
            }

            // Get BonyanIoMapping from node metadata
            if (!node.Metadata.TryGetValue("BonyanIoMapping", out var ioMappingObj) || 
                ioMappingObj is not BonyanIoMapping ioMapping)
            {
                Console.WriteLine($"[IoMapping] No BonyanIoMapping found for ElementId: {evt.ElementId}");
                return;
            }

            // Get process variables
            var processState = _processStateStore.Get(context.InstanceId);
            if (processState == null)
            {
                Console.WriteLine($"[IoMapping] ProcessState not found for InstanceId: {context.InstanceId}");
                return;
            }

            Console.WriteLine($"[IoMapping] Applying input mappings for ElementId: {evt.ElementId}");
            Console.WriteLine($"[IoMapping] Process variables before mapping: {string.Join(", ", processState.Variables.Select(kv => $"{kv.Key}={kv.Value}"))}");
            Console.WriteLine($"[IoMapping] Node variables before mapping: {string.Join(", ", context.LocalVariables.Select(kv => $"{kv.Key}={kv.Value}"))}");

            // Apply input mappings: Process Variables → Node Variables
            var result = _ioMappingApplier.ApplyInputs(
                ioMapping,
                processState.Variables,
                context.LocalVariables
            );

            if (result.Errors.Count > 0)
            {
                Console.WriteLine($"[IoMapping] Errors during input mapping: {string.Join("; ", result.Errors)}");
            }

            Console.WriteLine($"[IoMapping] Applied {result.AppliedMappings.Count} input mappings:");
            foreach (var mapping in result.AppliedMappings)
            {
                Console.WriteLine($"[IoMapping]   {mapping.SourceVariable} → {mapping.TargetVariable} = {mapping.Value}");
            }

            Console.WriteLine($"[IoMapping] Node variables after input mapping: {string.Join(", ", context.LocalVariables.Select(kv => $"{kv.Key}={kv.Value}"))}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[IoMapping] Error applying input mappings: {ex.Message}");
            Console.WriteLine($"[IoMapping] Stack trace: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// Executes C# script using Microsoft.CodeAnalysis.CSharp.Scripting.
    /// Applies input mappings before execution and extracts variables after execution.
    /// </summary>
    private async Task<Dictionary<string, object?>> ExecuteCSharpScriptAsync(string script, string? scriptFormat, ExecutionContext context)
    {
        var setVariables = new Dictionary<string, object?>();
        
        if (string.IsNullOrWhiteSpace(script))
        {
            Console.WriteLine($"[ExecuteCSharpScript] WARNING: Script is null or empty!");
            return setVariables;
        }

        // Only execute C# scripts
        if (scriptFormat != null && !scriptFormat.Equals("csharp", StringComparison.OrdinalIgnoreCase) && 
            !scriptFormat.Equals("c#", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"[ExecuteCSharpScript] WARNING: Script format '{scriptFormat}' is not C#. Skipping execution.");
            return setVariables;
        }

        Console.WriteLine($"[ExecuteCSharpScript] Executing C# script for ElementId: {context.CurrentElementId}");
        Console.WriteLine($"[ExecuteCSharpScript] Script content: {script.Substring(0, Math.Min(200, script.Length))}...");
        Console.WriteLine($"[ExecuteCSharpScript] LocalVariables before script: {string.Join(", ", context.LocalVariables.Select(kv => $"{kv.Key}={kv.Value}"))}");

        try
        {
            // Create ExpandoObject with all variables as direct properties
            var expando = new ExpandoObject();
            var expandoDict = (IDictionary<string, object?>)expando;
            
            // Add all variables from context to ExpandoObject
            foreach (var kv in context.LocalVariables)
            {
                expandoDict[kv.Key] = kv.Value;
            }
            
            // Add Variables property that points to the same ExpandoObject
            // This allows both direct access (baseScore) and Variables.property syntax
            expandoDict["Variables"] = expando;
            
            // Wrap script to enable dynamic binding for direct property access
            // Use 'dynamic g = globals;' at the start to enable direct property access
            // This allows scripts to use: var x = g.baseScore; instead of Variables.baseScore
            // But we want to support: var x = baseScore; so we need to create local variables
            var wrappedScript = CreateWrappedScript(script, context.LocalVariables.Keys);
            
            // Configure script options
            var scriptOptions = ScriptOptions.Default
                .WithImports(
                    "System",
                    "System.Dynamic",
                    "System.Collections.Generic",
                    "System.Linq",
                    "System.Text")
                .WithReferences(
                    typeof(object).Assembly,
                    typeof(Enumerable).Assembly,
                    typeof(ExpandoObject).Assembly);

            // Execute the script with ExpandoObject as globals
            // The wrapped script uses dynamic binding for property access
            var scriptState = await CSharpScript.RunAsync(wrappedScript, scriptOptions, expando);
            
            // Sync ExpandoObject changes back to context.LocalVariables
            foreach (var kv in expandoDict)
            {
                // Skip the Variables property itself
                if (kv.Key == "Variables")
                {
                    continue;
                }
                context.LocalVariables[kv.Key] = kv.Value;
            }
            
            Console.WriteLine($"[ExecuteCSharpScript] Script executed successfully");
            Console.WriteLine($"[ExecuteCSharpScript] LocalVariables after script: {string.Join(", ", context.LocalVariables.Select(kv => $"{kv.Key}={kv.Value}"))}");

            // Extract all variables that were set/modified during script execution
            // Variables are already in context.LocalVariables, so we return them
            foreach (var kv in context.LocalVariables)
            {
                setVariables[kv.Key] = kv.Value;
            }

            Console.WriteLine($"[ExecuteCSharpScript] Extracted {setVariables.Count} variables from script execution");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ExecuteCSharpScript] ERROR executing C# script: {ex.Message}");
            Console.WriteLine($"[ExecuteCSharpScript] Stack trace: {ex.StackTrace}");
            throw new InvalidOperationException($"Error executing C# script for ElementId {context.CurrentElementId}: {ex.Message}", ex);
        }

        return setVariables;
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

    /// <summary>
    /// Wraps the script to enable direct property access via dynamic binding.
    /// Creates local variables from globals at the start of the script.
    /// </summary>
    private string CreateWrappedScript(string script, IEnumerable<string> variableNames)
    {
        // Create local variable declarations from globals
        var variableDeclarations = new StringBuilder();
        variableDeclarations.AppendLine("dynamic g = globals;");
        
        // Add Variables as a local variable for Variables.property syntax
        variableDeclarations.AppendLine("dynamic Variables = g.Variables;");
        
        foreach (var varName in variableNames)
        {
            // Skip reserved names and Variables property (already added above)
            if (varName == "Variables" || IsReservedKeyword(varName))
            {
                continue;
            }
            
            // Create local variable: var baseScore = g.baseScore;
            variableDeclarations.AppendLine($"var {varName} = g.{varName};");
        }
        
        // Combine declarations with original script
        return $"{variableDeclarations}\n{script}";
    }
    
    /// <summary>
    /// Checks if a name is a C# reserved keyword.
    /// </summary>
    private bool IsReservedKeyword(string name)
    {
        var keywords = new HashSet<string>
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
            "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
            "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
            "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
            "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
            "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
            "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual",
            "void", "volatile", "while", "var", "dynamic"
        };
        return keywords.Contains(name.ToLower());
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
