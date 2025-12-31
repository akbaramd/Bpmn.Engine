using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Jint;
using Jint.Native;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.Exceptions;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.Services;

public sealed class MultiLanguageScriptTaskExecutor : IScriptTaskExecutor
{
    private static readonly HashSet<string> CSharpFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "c#", "csharp", "text/x-csharp"
    };

    private static readonly HashSet<string> JavaScriptFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "javascript", "js", "ecmascript", "text/javascript", "application/javascript"
    };

    private readonly ILogger<MultiLanguageScriptTaskExecutor> _logger;
    private readonly MultiLanguageScriptTaskExecutorOptions _options;
    private readonly IJsonSerializer _jsonSerializer;

    private readonly ConcurrentDictionary<string, Lazy<ScriptRunner<object>>> _csharpCache
        = new(StringComparer.Ordinal);

    private readonly ScriptOptions _csharpOptions;
    public MultiLanguageScriptTaskExecutor(
        ILogger<MultiLanguageScriptTaskExecutor> logger,
        MultiLanguageScriptTaskExecutorOptions options,
        IJsonSerializer jsonSerializer)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _jsonSerializer = jsonSerializer ?? throw new ArgumentNullException(nameof(jsonSerializer));

        _csharpOptions = BuildScriptOptions(
            typeof(ScriptGlobals).Assembly,
            typeof(ScriptExecutionContext).Assembly,
            typeof(Process).Assembly,
            typeof(Token).Assembly,
            typeof(BpmnErrorException).Assembly,
            typeof(MultiLanguageScriptTaskExecutor).Assembly);
    }

    public async Task ExecuteAsync(Process process, Token token, BpmnScriptTask task, CancellationToken ct)
    {
        if (process is null) throw new ArgumentNullException(nameof(process));
        if (token is null) throw new ArgumentNullException(nameof(token));
        if (task is null) throw new ArgumentNullException(nameof(task));

        var taskId = task.id;
        if (string.IsNullOrWhiteSpace(taskId))
        {
            token.Fail("ScriptTask.id is null/empty.");
            return;
        }

        var format = GetScriptFormat(task)?.Trim();

        if (string.IsNullOrWhiteSpace(format))
        {
            if (!_options.TreatNullFormatAsCSharp)
            {
                token.Fail($"ScriptTask '{taskId}' has empty scriptFormat.");
                return;
            }
            format = "c#";
        }

        var code = GetScriptCode(task)?.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            token.Fail($"ScriptTask '{taskId}' has empty script body.");
            return;
        }

        if (IsCSharp(format))
        {
            await ExecuteCSharpAsync(process, token, taskId!, code!, ct);
            return;
        }

        if (IsJavaScript(format))
        {
            ExecuteJavaScriptWithContext(process, token, taskId!, code!, ct);
            return;
        }

        token.Fail($"Unsupported ScriptTask scriptFormat='{format}' (TaskId={taskId}).");
    }

    // -----------------------------
    // C# (Roslyn) - context is live (write-through)
    // -----------------------------
 // -----------------------------
    // C# (Roslyn) - compiled + cached + timeout only for execution
    // -----------------------------
    private async Task ExecuteCSharpAsync(Process process, Token token, string taskId, string code, CancellationToken ct)
    {
        _logger.LogInformation(
            "[SCRIPT-EXEC] Starting C# script execution. TaskId={TaskId} ProcessId={ProcessId} TokenId={TokenId}",
            taskId, process.Id, token.Id);

        var tokenVarsBefore = token.Variables.ToDictionary(kv => kv.Key, kv => kv.Value);
        _logger.LogDebug(
            "[SCRIPT-EXEC] Token variables BEFORE execution. TaskId={TaskId} Count={Count} Variables={Variables}",
            taskId,
            tokenVarsBefore.Count,
            string.Join(", ", tokenVarsBefore.Select(kv => $"{kv.Key}={kv.Value}")));

        var cacheKey = $"{taskId}:{Sha256(code)}";

        ScriptRunner<object> runner;
        try
        {
            // ✅ Compile + CreateDelegate cached (NO timeout here)
            runner = _csharpCache.GetOrAdd(
                cacheKey,
                _ => new Lazy<ScriptRunner<object>>(
                    () => CompileCSharp(code, _csharpOptions),
                    LazyThreadSafetyMode.ExecutionAndPublication
                )
            ).Value;
        }
        catch (CompilationErrorException cex)
        {
            var errors = string.Join(Environment.NewLine, cex.Diagnostics.Select(d => d.ToString()));
            _logger.LogError("C# ScriptTask compilation failed. TaskId={TaskId}\n{Errors}", taskId, errors);

            throw new TokenExecutionException(
                process.Id,
                token.Id,
                taskId,
                $"C# ScriptTask '{taskId}' compilation failed.",
                cex);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_options.CSharpTimeout);

        try
        {
            var globals = new ScriptGlobals(process, token);

            _logger.LogDebug(
                "[SCRIPT-EXEC] Executing C# script. TaskId={TaskId} CodeLength={CodeLength}",
                taskId, code.Length);

            // ✅ Only runtime execution is time-boxed
            await runner(globals, timeoutCts.Token);

            // ✅ Sync variables back to token
            globals.SyncToToken(token);

            var tokenVarsAfter = token.Variables.ToDictionary(kv => kv.Key, kv => kv.Value);
            _logger.LogInformation("[SCRIPT-EXEC] ✅ Script execution completed successfully. TaskId={TaskId}", taskId);
            _logger.LogDebug(
                "[SCRIPT-EXEC] Token variables AFTER execution. TaskId={TaskId} Count={Count} Variables={Variables}",
                taskId,
                tokenVarsAfter.Count,
                string.Join(", ", tokenVarsAfter.Select(kv => $"{kv.Key}={kv.Value}")));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // upstream cancellation
            throw;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new TokenExecutionException(
                process.Id,
                token.Id,
                taskId,
                $"C# ScriptTask '{taskId}' timed out after {_options.CSharpTimeout.TotalSeconds:0.#}s.");
        }
        catch (BpmnErrorException)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (ex.Message.StartsWith("BPMN Error ", StringComparison.OrdinalIgnoreCase))
            {
                var parts = ex.Message.Substring("BPMN Error ".Length).Split(new[] { ':' }, 2);
                var errorCode = parts.Length > 0 ? parts[0].Trim() : "UNKNOWN_ERROR";
                var errorMessage = parts.Length > 1 ? parts[1].Trim() : ex.Message;

                throw new BpmnErrorException(errorCode, errorMessage, ex);
            }

            _logger.LogError(
                ex,
                "[SCRIPT-EXEC] ❌ C# ScriptTask execution failed (technical error). TaskId={TaskId} Message={Message}",
                taskId, ex.Message);

            throw new TokenExecutionException(
                process.Id,
                token.Id,
                taskId,
                $"C# ScriptTask '{taskId}' failed: {ex.Message}",
                ex);
        }
    }

    private static ScriptRunner<object> CompileCSharp(string code, ScriptOptions options)
    {
        // ✅ IMPORTANT: globalsType must be provided
        var script = CSharpScript.Create(
            code,
            options,
            globalsType: typeof(ScriptGlobals));

        // ✅ Compile now (fail fast). No cancellation token here.
        var diags = script.Compile();
        var errors = diags.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors.Length > 0)
            throw new CompilationErrorException("C# script compilation failed.", errors.ToImmutableArray());

        // ✅ Create delegate => executor built now (not during Run)
        return script.CreateDelegate();
    }
private static ScriptOptions BuildScriptOptions(params Assembly[] extraAssemblies)
{
    var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    // ✅ framework refs (System.Runtime, mscorlib, ...)
    var tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
    if (!string.IsNullOrWhiteSpace(tpa))
    {
        foreach (var p in tpa.Split(Path.PathSeparator))
            if (!string.IsNullOrWhiteSpace(p))
                paths.Add(p);
    }

    // ✅ app refs (skip dynamic + empty location)
    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies().Concat(extraAssemblies))
    {
        if (asm == null) continue;
        if (asm.IsDynamic) continue;

        string? loc = null;
        try { loc = asm.Location; } catch { }

        if (string.IsNullOrWhiteSpace(loc)) continue;
        paths.Add(loc);
    }

    var refs = paths.Select(p => (MetadataReference)MetadataReference.CreateFromFile(p));

    return ScriptOptions.Default
        .WithReferences(refs)
        .WithImports(
            "System",
            "System.Threading",
            "System.Threading.Tasks",
            "System.Linq",
            "System.Collections",
            "System.Collections.Generic",
            "System.Text",
            "System.Globalization",
            "System.Text.RegularExpressions",

            "Novin.Bpmn.Engine.Domain.Entities",
            "Novin.Bpmn.Engine.Domain.Exceptions",
            "Novin.Bpmn.Engine.Application.Services",
            "Novin.Bpmn.Engine.Application.Common.Interfaces",
            "Novin.Bpmn.Models.Models"
        );
}


    // -----------------------------
    // JavaScript (Jint)
    // - Provide JS object: context = { Variables: {...} }
    // - Variables points to token locals ONLY (mutable)
    // - No direct access to process variables (all via mapping)
    // - After execution, sync token variables back to domain
    // -----------------------------
    
    private sealed class JsContext
    {
        /// <summary>
        /// Token local variables (mutable) - only source of variables for scripts.
        /// </summary>
        public IDictionary<string, object?> Variables { get; }

        /// <summary>
        /// Alias for Variables (backward compatibility).
        /// </summary>
        public IDictionary<string, object?> TokenVariables => Variables;

        public JsContext(IDictionary<string, object?> tokenVariables)
        {
            Variables = tokenVariables ?? throw new ArgumentNullException(nameof(tokenVariables));
        }
    }

    private void ExecuteJavaScriptWithContext(Process process, Token token, string taskId, string code, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
            throw new OperationCanceledException(ct);

        // ---- safe defaults if options are misconfigured (0/negative) ----
        var timeout = _options.JavaScriptTimeout <= TimeSpan.Zero
            ? TimeSpan.FromSeconds(2)
            : _options.JavaScriptTimeout;

        var maxStatements = _options.JavaScriptMaxStatements <= 0
            ? 10_000
            : _options.JavaScriptMaxStatements;

        var maxMemory = _options.JavaScriptMaxMemoryBytes <= 0
            ? 4_000_000
            : _options.JavaScriptMaxMemoryBytes;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        // ✅ فقط token locals - هیچ دسترسی به process variables وجود ندارد
        var tokVars = token.Variables.ToDictionary(kv => kv.Key, kv => (object?)kv.Value);

        try
        {
            // IMPORTANT: use only CancellationToken for timeout (no TimeoutInterval)
            var engine = new Jint.Engine(o =>
            {
                o.LimitMemory(maxMemory);
                o.MaxStatements(maxStatements);
                o.CancellationToken(timeoutCts.Token);
            });

            // ✅ فقط token locals
            var ctxObj = new JsContext(tokVars);

            engine.SetValue("context", ctxObj);
            engine.SetValue("variables", tokVars); // alias for token locals
            engine.SetValue("tokenVariables", tokVars); // backward compatibility
            engine.SetValue("log", new Action<object?>(m => _logger.LogInformation("[JS] {Msg}", m)));

            engine.Execute(code);

            // ✅ Sync back: only token variables (process sync happens via ApplyOutputs)
            foreach (var (k, v) in tokVars)
                token.SetVariable(k, NormalizeJs(v)!);
        }
        catch (Jint.Runtime.ExecutionCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            // Throw TokenExecutionException instead of calling token.Fail() directly
            // This allows the orchestrator to handle it properly (create incident, fail token in separate transaction)
            throw new TokenExecutionException(
                process.Id,
                token.Id,
                taskId,
                $"JS ScriptTask '{taskId}' timed out after {timeout.TotalSeconds:0.#}s.");
        }
        catch (Jint.Runtime.ExecutionCanceledException) when (ct.IsCancellationRequested)
        {
            // upstream cancellation - rethrow as-is
            throw new OperationCanceledException(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "JS ScriptTask failed. TaskId={TaskId}, Timeout={TimeoutMs}, MaxStatements={MaxStatements}, MaxMemory={MaxMemory}",
                taskId, timeout.TotalMilliseconds, maxStatements, maxMemory);

            // Throw TokenExecutionException instead of calling token.Fail() directly
            throw new TokenExecutionException(
                process.Id,
                token.Id,
                taskId,
                $"JS ScriptTask '{taskId}' failed: {ex.Message}",
                ex);
        }

        static object? NormalizeJs(object? value)
            => value is JsValue jsv ? jsv.ToObject() : value;
    }

    private void ApplyJsonVariablesToProcess(Process process, string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;

        var dict = _jsonSerializer.DeserializeObject<Dictionary<string,string>>(json);
        if (dict == null) return;

        foreach (var (k, v) in dict)
            process.SetVariable(k, v);
    }

    private void ApplyJsonVariablesToToken(Token token, string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;

        var dict = _jsonSerializer.DeserializeObject<Dictionary<string, JsonElement>>(json);
        if (dict == null) return;

        foreach (var (k, v) in dict)
            token.SetVariable(k, ConvertJson(v)!);
    }

    private static object? ConvertJson(JsonElement e) =>
        e.ValueKind switch
        {
            JsonValueKind.String => e.GetString(),
            JsonValueKind.Number => e.TryGetInt64(out var l) ? l : e.TryGetDouble(out var d) ? d : e.ToString(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            // objects/arrays: keep as JsonElement (or map to Dictionary if you want)
            _ => e
        };

    // -----------------------------
    // Helpers
    // -----------------------------
    private static bool IsCSharp(string format) => CSharpFormats.Contains(format);
    private static bool IsJavaScript(string format) => JavaScriptFormats.Contains(format);

    private static string Sha256(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }

    // Robust reflection getters (your model differs: Script.Value / script / ScriptFormat etc.)
    private static string? GetScriptFormat(BpmnScriptTask task)
    {
        var t = task.GetType();
        return t.GetProperty("scriptFormat")?.GetValue(task) as string
               ?? t.GetProperty("ScriptFormat")?.GetValue(task) as string;
    }

    private static string? GetScriptCode(BpmnScriptTask task)
    {
        var t = task.GetType();

        var direct = t.GetProperty("script")?.GetValue(task);
        if (direct is string s1) return s1;

        var scriptObj = t.GetProperty("Script")?.GetValue(task);
        if (scriptObj is string s2) return s2;

        if (scriptObj != null)
        {
            var v = scriptObj.GetType().GetProperty("Value")?.GetValue(scriptObj) as string;
            if (!string.IsNullOrWhiteSpace(v)) return v;
        }

        return t.GetProperty("script1")?.GetValue(task) as string;
    }
}