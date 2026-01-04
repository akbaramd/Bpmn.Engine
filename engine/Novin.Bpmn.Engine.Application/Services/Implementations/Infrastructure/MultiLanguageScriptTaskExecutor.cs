using System;
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
using Microsoft.Extensions.Logging;
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
            throw new ScriptTaskExecutionException(
                process.Id, token.Id, taskId: "<null>",
                message: "ScriptTask.id is null/empty.",
                kind: EngineErrorKind.Logical);
        }

        var format = (GetScriptFormat(task) ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(format))
        {
            if (!_options.TreatNullFormatAsCSharp)
            {
                throw new ScriptTaskExecutionException(
                    process.Id, token.Id, taskId!,
                    $"ScriptTask '{taskId}' has empty scriptFormat.",
                    EngineErrorKind.Logical);
            }

            format = "c#";
        }

        var code = (GetScriptCode(task) ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ScriptTaskExecutionException(
                process.Id, token.Id, taskId!,
                $"ScriptTask '{taskId}' has empty script body.",
                EngineErrorKind.Logical);
        }

        if (IsCSharp(format))
        {
            await ExecuteCSharpAsync(process, token, taskId!, code, ct);
            return;
        }

        if (IsJavaScript(format))
        {
            ExecuteJavaScriptWithContext(process, token, taskId!, code, ct);
            return;
        }

        throw new ScriptTaskExecutionException(
            process.Id, token.Id, taskId!,
            $"Unsupported ScriptTask scriptFormat='{format}' (TaskId={taskId}).",
            EngineErrorKind.Logical);
    }

    // -----------------------------
    // C# (Roslyn) - compiled + cached + execution timeout only
    // -----------------------------
    private async Task ExecuteCSharpAsync(Process process, Token token, string taskId, string codes, CancellationToken ct)
    {
        _logger.LogInformation(
            "[SCRIPT-EXEC] Starting C# script execution. TaskId={TaskId} ProcessId={ProcessId} TokenId={TokenId}",
            taskId, process.Id, token.Id);

        var cacheKey = $"{taskId}:{Sha256(codes)}";

        ScriptRunner<object> runner;
        try
        {
            runner = _csharpCache.GetOrAdd(
                cacheKey,
                _ => new Lazy<ScriptRunner<object>>(
                    () => CompileCSharp(codes, _csharpOptions),
                    LazyThreadSafetyMode.ExecutionAndPublication
                )
            ).Value;
        }
        catch (CompilationErrorException cex)
        {
            var errors = string.Join(Environment.NewLine, cex.Diagnostics.Select(d => d.ToString()));
            _logger.LogError("[SCRIPT-EXEC] C# compilation failed. TaskId={TaskId}\n{Errors}", taskId, errors);

            throw new ScriptTaskExecutionException(
                process.Id, token.Id, taskId,
                $"C# ScriptTask '{taskId}' compilation failed.",
                EngineErrorKind.Logical,
                inner: cex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SCRIPT-EXEC] Unexpected error while preparing C# runner. TaskId={TaskId}", taskId);

            throw new ScriptTaskExecutionException(
                process.Id, token.Id, taskId,
                $"C# ScriptTask '{taskId}' preparation failed: {ex.Message}",
                EngineErrorKind.Technical,
                inner: ex);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var timeout = _options.CSharpTimeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(2) : _options.CSharpTimeout;
        timeoutCts.CancelAfter(timeout);

        try
        {
            var globals = new ScriptGlobals(process, token);

            await runner(globals, timeoutCts.Token);

            // ✅ Sync back to token locals
            globals.SyncToToken(token);

            _logger.LogInformation("[SCRIPT-EXEC] ✅ C# script completed. TaskId={TaskId}", taskId);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // upstream cancellation
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new ScriptTaskExecutionException(
                process.Id, token.Id, taskId,
                $"C# ScriptTask '{taskId}' timed out after {timeout.TotalSeconds:0.#}s.",
                EngineErrorKind.Technical);
        }
        catch (BpmnErrorException bex)
        {
            // ✅ explicit BPMN error semantics (catchable boundary error)
            throw new ScriptTaskExecutionException(
                process.Id, token.Id, taskId,
                $"BPMN Error '{bex.Code}': {bex.Message}",
                EngineErrorKind.BpmnError,
                bpmnErrorCode: bex.Code,
                inner: bex);
        }
        catch (Exception ex)
        {
            // Optional compatibility: allow "BPMN Error CODE: msg" text-based throw
            if (TryParseBpmnErrorFromMessage(ex.Message, out var code, out var msg))
            {
                throw new ScriptTaskExecutionException(
                    process.Id, token.Id, taskId,
                    $"BPMN Error '{codes}': {msg}",
                    EngineErrorKind.BpmnError,
                    bpmnErrorCode: code,
                    inner: ex);
            }

            _logger.LogError(ex,
                "[SCRIPT-EXEC] ❌ C# ScriptTask runtime failed. TaskId={TaskId} Message={Message}",
                taskId, ex.Message);

            throw new ScriptTaskExecutionException(
                process.Id, token.Id, taskId,
                $"C# ScriptTask '{taskId}' failed: {ex.Message}",
                EngineErrorKind.Technical,
                inner: ex);
        }
    }

    private static ScriptRunner<object> CompileCSharp(string code, ScriptOptions options)
    {
        var script = CSharpScript.Create(
            code,
            options,
            globalsType: typeof(ScriptGlobals));

        var diags = script.Compile();
        var errors = diags.Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors.Length > 0)
            throw new CompilationErrorException("C# script compilation failed.", errors.ToImmutableArray());

        return script.CreateDelegate();
    }

    private static ScriptOptions BuildScriptOptions(params Assembly[] extraAssemblies)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (!string.IsNullOrWhiteSpace(tpa))
        {
            foreach (var p in tpa.Split(Path.PathSeparator))
                if (!string.IsNullOrWhiteSpace(p))
                    paths.Add(p);
        }

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies().Concat(extraAssemblies))
        {
            if (asm == null || asm.IsDynamic) continue;

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
    // - Token locals only (mutable)
    // - After execution, sync back to token locals
    // -----------------------------
    private sealed class JsContext
    {
        public IDictionary<string, object?> Variables { get; }
        public IDictionary<string, object?> TokenVariables => Variables;

        public JsContext(IDictionary<string, object?> tokenVariables)
        {
            Variables = tokenVariables ?? throw new ArgumentNullException(nameof(tokenVariables));
        }
    }

    private void ExecuteJavaScriptWithContext(Process process, Token token, string taskId, string codes, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
            throw new OperationCanceledException(ct);

        var timeout = _options.JavaScriptTimeout <= TimeSpan.Zero ? TimeSpan.FromSeconds(2) : _options.JavaScriptTimeout;
        var maxStatements = _options.JavaScriptMaxStatements <= 0 ? 10_000 : _options.JavaScriptMaxStatements;
        var maxMemory = _options.JavaScriptMaxMemoryBytes <= 0 ? 4_000_000 : _options.JavaScriptMaxMemoryBytes;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        // token locals only
        var tokVars = token.Variables.ToDictionary(kv => kv.Key, kv => (object?)kv.Value);

        try
        {
            var engine = new Jint.Engine(o =>
            {
                o.LimitMemory(maxMemory);
                o.MaxStatements(maxStatements);
                o.CancellationToken(timeoutCts.Token);
            });

            var ctxObj = new JsContext(tokVars);

            engine.SetValue("context", ctxObj);
            engine.SetValue("variables", tokVars);
            engine.SetValue("tokenVariables", tokVars);
            engine.SetValue("log", new Action<object?>(m => _logger.LogInformation("[JS] {Msg}", m)));

            engine.Execute(codes);

            // sync back
            foreach (var (k, v) in tokVars)
                token.SetVariable(k, NormalizeJs(v));
        }
        catch (Jint.Runtime.ExecutionCanceledException) when (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new ScriptTaskExecutionException(
                process.Id, token.Id, taskId,
                $"JS ScriptTask '{taskId}' timed out after {timeout.TotalSeconds:0.#}s.",
                EngineErrorKind.Technical);
        }
        catch (Jint.Runtime.ExecutionCanceledException) when (ct.IsCancellationRequested)
        {
            throw new OperationCanceledException(ct);
        }
        catch (Exception ex)
        {
            // Optional: allow throwing "BPMN Error CODE: msg" from JS
            if (TryParseBpmnErrorFromMessage(ex.Message, out var code, out var msg))
            {
                throw new ScriptTaskExecutionException(
                    process.Id, token.Id, taskId,
                    $"BPMN Error '{code}': {msg}",
                    EngineErrorKind.BpmnError,
                    bpmnErrorCode: code,
                    inner: ex);
            }

            _logger.LogError(ex,
                "[SCRIPT-EXEC] JS ScriptTask failed. TaskId={TaskId} TimeoutMs={TimeoutMs} MaxStatements={MaxStatements} MaxMemory={MaxMemory}",
                taskId, timeout.TotalMilliseconds, maxStatements, maxMemory);

            throw new ScriptTaskExecutionException(
                process.Id, token.Id, taskId,
                $"JS ScriptTask '{taskId}' failed: {ex.Message}",
                EngineErrorKind.Technical,
                inner: ex);
        }

        static object? NormalizeJs(object? value)
            => value is JsValue jsv ? jsv.ToObject() : value;
    }

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

    private static bool TryParseBpmnErrorFromMessage(string? message, out string code, out string msg)
    {
        code = "";
        msg = "";

        if (string.IsNullOrWhiteSpace(message)) return false;

        // Pattern: "BPMN Error CODE: message"
        const string prefix = "BPMN Error ";
        if (!message.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var rest = message.Substring(prefix.Length);
        var parts = rest.Split(new[] { ':' }, 2);

        var c = (parts.Length > 0 ? parts[0] : "").Trim();
        if (string.IsNullOrWhiteSpace(c)) return false;

        code = c;
        msg = (parts.Length > 1 ? parts[1] : rest).Trim();
        if (string.IsNullOrWhiteSpace(msg)) msg = message;

        return true;
    }

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
