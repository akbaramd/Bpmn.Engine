using System.Collections;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Jint;
using Jint.Native;
using Jint.Runtime;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.Exceptions;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.Services;

public sealed class ScriptGlobals
{
    // User wants: context.Variables / context.TokenVariables
    public ScriptExecutionContext context { get; }

    // Optional aliases (helpful)
    public ScriptVariableBag variables => context.Variables;
    public ScriptVariableBag tokenVariables => context.TokenVariables;

    public Process Process { get; }
    public Token Token { get; }

    public ScriptGlobals(Process process, Token token)
    {
        Process = process ?? throw new ArgumentNullException(nameof(process));
        Token = token ?? throw new ArgumentNullException(nameof(token));
        context = new ScriptExecutionContext(Process, Token);
    }
}

public sealed class TokenVariableBag : IReadOnlyDictionary<string, object?>
{
    private readonly Token _token;

    public TokenVariableBag(Token token) => _token = token;

    public object? this[string key]
    {
        get => _token.TryGetVariable(key, out var v) ? v : null;
        set => _token.SetVariable(key, value!); // value can be null depending on policy; store as null object ok
    }

    public IEnumerable<string> Keys => _token.Variables.Keys;
    public IEnumerable<object?> Values => _token.Variables.Values.Cast<object?>();
    public int Count => _token.Variables.Count;

    public bool ContainsKey(string key) => _token.HasVariable(key);
  

    public bool TryGetValue(string key, out object? value) => _token.TryGetVariable(key, out value);

    public object? GetValueOrDefault(string key) => this[key];

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        => _token.Variables.Select(kv => new KeyValuePair<string, object?>(kv.Key, kv.Value)).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
public sealed class ScriptVariableBag
{
    private readonly Func<string, bool> _has;
    private readonly Func<string, object?> _getOrNull;
    private readonly Action<string, object?>? _set; // nullable for read-only bags
    private readonly Func<IEnumerable<string>>? _getKeys; // nullable - only for token bags
    private readonly Func<int>? _getCount; // nullable - only for token bags

    private ScriptVariableBag(
        Func<string, bool> has,
        Func<string, object?> getOrNull,
        Action<string, object?>? set,
        Func<IEnumerable<string>>? getKeys = null,
        Func<int>? getCount = null)
    {
        _has = has;
        _getOrNull = getOrNull;
        _set = set;
        _getKeys = getKeys;
        _getCount = getCount;
    }

    public object? this[string key]
    {
        get => _getOrNull(key);
        set
        {
            if (_set == null)
                throw new InvalidOperationException("This ScriptVariableBag is read-only.");
            _set(key, value);
        }
    }

    public bool Contains(string key) => _has(key);

    public IEnumerable<string> Keys => _getKeys?.Invoke() ?? Enumerable.Empty<string>();

    public int Count => _getCount?.Invoke() ?? 0;

    public static ScriptVariableBag ForProcess(Process p) =>
        new(
            has: p.HasVariable,
            getOrNull: k => p.HasVariable(k) ? p.GetVariable(k) : null,
            set: (k, v) => p.SetVariable(k, v!));

    public static ScriptVariableBag ForToken(Token t) =>
        new(
            has: t.HasVariable,
            getOrNull: k => t.TryGetVariable(k, out var v) ? v : null,
            set: (k, v) => t.SetVariable(k, v!),
            getKeys: () => t.Variables.Keys,
            getCount: () => t.Variables.Count);
}
// =============================
public sealed class ScriptExecutionContext
{
    /// <summary>
    /// Token local variables (mutable).
    /// Scripts should ONLY use this for reading/writing variables.
    /// These variables are mapped from process via ApplyInputs before execution,
    /// and synced back to process via ApplyOutputs after execution.
    /// </summary>
    public ScriptVariableBag Variables { get; }

    /// <summary>
    /// Alias for Variables (backward compatibility).
    /// </summary>
    public ScriptVariableBag TokenVariables => Variables;

    public ScriptExecutionContext(Process process, Token token)
    {
        // ✅ فقط token locals - هیچ دسترسی مستقیم به process variables وجود ندارد
        // همه متغیرها باید از طریق mapping (ApplyInputs/ApplyOutputs) مدیریت شوند
        Variables = ScriptVariableBag.ForToken(token);
    }
}

public sealed class MultiLanguageScriptTaskExecutorOptions
{
    // If scriptFormat is null/empty => treat as C#
    public bool TreatNullFormatAsCSharp { get; init; } = true;

    public TimeSpan CSharpTimeout { get; init; } = TimeSpan.FromSeconds(2);

    public TimeSpan JavaScriptTimeout { get; init; } = TimeSpan.FromSeconds(2);
    public int JavaScriptMaxStatements { get; init; } = 10_000;
    public long JavaScriptMaxMemoryBytes { get; init; } = 4_000_000; // 4MB
}
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

    private readonly ConcurrentDictionary<string, ScriptRunner<object?>> _csharpCache = new(StringComparer.Ordinal);

    public MultiLanguageScriptTaskExecutor(
        ILogger<MultiLanguageScriptTaskExecutor> logger,
        MultiLanguageScriptTaskExecutorOptions options)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
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
    private async Task ExecuteCSharpAsync(Process process, Token token, string taskId, string code, CancellationToken ct)
    {
        _logger.LogInformation(
            "[SCRIPT-EXEC] Starting C# script execution. TaskId={TaskId} ProcessId={ProcessId} TokenId={TokenId}",
            taskId,
            process.Id,
            token.Id);

        // Log token variables before execution
        var tokenVarsBefore = token.Variables.ToDictionary(kv => kv.Key, kv => kv.Value);
        _logger.LogDebug(
            "[SCRIPT-EXEC] Token variables BEFORE execution. TaskId={TaskId} Count={Count} Variables={Variables}",
            taskId,
            tokenVarsBefore.Count,
            string.Join(", ", tokenVarsBefore.Select(kv => $"{kv.Key}={kv.Value}")));

        var cacheKey = $"{taskId}:{Sha256(code)}";
        var runner = _csharpCache.GetOrAdd(cacheKey, _ => CompileCSharp(code));

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_options.CSharpTimeout);

        try
        {
            var globals = new ScriptGlobals(process, token);

            _logger.LogDebug(
                "[SCRIPT-EXEC] Executing script. TaskId={TaskId} CodeLength={CodeLength}",
                taskId,
                code.Length);

            // C# script can use:
            // context.Variables["amount"] = 120;
            // context.TokenVariables["step"] = "x";
            await runner(globals, timeoutCts.Token);

            // Log token variables after successful execution
            var tokenVarsAfter = token.Variables.ToDictionary(kv => kv.Key, kv => kv.Value);
            _logger.LogInformation(
                "[SCRIPT-EXEC] ✅ Script execution completed successfully. TaskId={TaskId}",
                taskId);
            _logger.LogDebug(
                "[SCRIPT-EXEC] Token variables AFTER execution. TaskId={TaskId} Count={Count} Variables={Variables}",
                taskId,
                tokenVarsAfter.Count,
                string.Join(", ", tokenVarsAfter.Select(kv => $"{kv.Key}={kv.Value}")));
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            // Throw TokenExecutionException instead of calling token.Fail() directly
            // This allows the orchestrator to handle it properly (create incident, fail token in separate transaction)
            throw new TokenExecutionException(
                process.Id,
                token.Id,
                taskId,
                $"C# ScriptTask '{taskId}' timed out after {_options.CSharpTimeout.TotalSeconds:0.#}s.");
        }
        catch (CompilationErrorException cex)
        {
            var errors = string.Join(Environment.NewLine, cex.Diagnostics.Select(d => d.ToString()));
            _logger.LogError("C# ScriptTask compilation failed. TaskId={TaskId}\n{Errors}", taskId, errors);
            // Throw TokenExecutionException instead of calling token.Fail() directly
            throw new TokenExecutionException(
                process.Id,
                token.Id,
                taskId,
                $"C# ScriptTask '{taskId}' compilation failed.",
                cex);
        }
        catch (BpmnErrorException bex)
        {
            // BPMN Error: propagate directly without wrapping
            _logger.LogWarning(
                "[SCRIPT-EXEC] ⚠️ C# ScriptTask threw BPMN Error. TaskId={TaskId} ErrorCode={ErrorCode} Message={Message}",
                taskId,
                bex.Code,
                bex.Message);
            
            // Log token variables when BPMN error occurs
            var tokenVarsOnError = token.Variables.ToDictionary(kv => kv.Key, kv => kv.Value);
            _logger.LogDebug(
                "[SCRIPT-EXEC] Token variables when BPMN error thrown. TaskId={TaskId} Count={Count} Variables={Variables}",
                taskId,
                tokenVarsOnError.Count,
                string.Join(", ", tokenVarsOnError.Select(kv => $"{kv.Key}={kv.Value}")));
            
            throw;
        }
        catch (Exception ex)
        {
            // Check if exception message indicates a BPMN Error
            // Format: "BPMN Error {errorCode}: {message}"
            if (ex.Message.StartsWith("BPMN Error ", StringComparison.OrdinalIgnoreCase))
            {
                var parts = ex.Message.Substring("BPMN Error ".Length).Split(new[] { ':' }, 2);
                var errorCode = parts.Length > 0 ? parts[0].Trim() : "UNKNOWN_ERROR";
                var errorMessage = parts.Length > 1 ? parts[1].Trim() : ex.Message;

                _logger.LogWarning(
                    "[SCRIPT-EXEC] ⚠️ C# ScriptTask threw exception with BPMN Error format. Converting to BpmnErrorException. TaskId={TaskId} ErrorCode={ErrorCode} Message={Message}",
                    taskId,
                    errorCode,
                    errorMessage);
                
                // Log token variables when converting to BPMN error
                var tokenVarsOnError = token.Variables.ToDictionary(kv => kv.Key, kv => kv.Value);
                _logger.LogDebug(
                    "[SCRIPT-EXEC] Token variables when converting to BPMN error. TaskId={TaskId} Count={Count} Variables={Variables}",
                    taskId,
                    tokenVarsOnError.Count,
                    string.Join(", ", tokenVarsOnError.Select(kv => $"{kv.Key}={kv.Value}")));

                throw new BpmnErrorException(errorCode, errorMessage, ex);
            }

            _logger.LogError(
                ex,
                "[SCRIPT-EXEC] ❌ C# ScriptTask execution failed (technical error). TaskId={TaskId} Message={Message}",
                taskId,
                ex.Message);
            
            // Log token variables on technical error
            var tokenVarsOnTechError = token.Variables.ToDictionary(kv => kv.Key, kv => kv.Value);
            _logger.LogDebug(
                "[SCRIPT-EXEC] Token variables when technical error occurred. TaskId={TaskId} Count={Count} Variables={Variables}",
                taskId,
                tokenVarsOnTechError.Count,
                string.Join(", ", tokenVarsOnTechError.Select(kv => $"{kv.Key}={kv.Value}")));
            
            // Throw TokenExecutionException instead of calling token.Fail() directly
            throw new TokenExecutionException(
                process.Id,
                token.Id,
                taskId,
                $"C# ScriptTask '{taskId}' failed: {ex.Message}",
                ex);
        }
    }

    private static ScriptRunner<object?> CompileCSharp(string code)
    {
        var options = ScriptOptions.Default
            .AddReferences(
                typeof(object).Assembly,                    // System.Private.CoreLib
                typeof(Console).Assembly,                   // System.Console
                typeof(Enumerable).Assembly,                 // System.Linq
                typeof(List<>).Assembly,                     // System.Collections
                typeof(Dictionary<,>).Assembly,             // System.Collections.Generic
                typeof(Convert).Assembly,                    // System.Runtime
                typeof(string).Assembly,                     // System.Runtime
                typeof(DateTime).Assembly,                  // System.Runtime
                typeof(InvalidOperationException).Assembly,  // System.Runtime
                typeof(Process).Assembly,                    // Domain entities
                typeof(Token).Assembly,                      // Domain entities
                typeof(ScriptGlobals).Assembly)             // Application services
            .AddImports(
                "System",
                "System.Linq",
                "System.Collections",
                "System.Collections.Generic",
                "System.Text",
                "System.Globalization",
                "Novin.Bpmn.Engine.Domain.Entities",
                "Novin.Bpmn.Engine.Domain.Exceptions",
                "Novin.Bpmn.Engine.Application.Services");

        var script = CSharpScript.Create(code, options, typeof(ScriptGlobals));
        script.Compile();
        return script.CreateDelegate();
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

    private static void ApplyJsonVariablesToProcess(Process process, string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;

        var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        if (dict == null) return;

        foreach (var (k, v) in dict)
            process.SetVariable(k, ConvertJson(v)!);
    }

    private static void ApplyJsonVariablesToToken(Token token, string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;

        var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
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

public interface IScriptTaskExecutor
{
    Task ExecuteAsync(Process process, Token token, BpmnScriptTask task, CancellationToken ct);
}

