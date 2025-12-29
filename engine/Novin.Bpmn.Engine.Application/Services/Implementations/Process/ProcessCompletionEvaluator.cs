using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.Services;

public sealed class ProcessCompletionEvaluator : IProcessCompletionEvaluator
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<ProcessCompletionEvaluator> _logger;

    public ProcessCompletionEvaluator(
        IUnitOfWork uow,
        ILogger<ProcessCompletionEvaluator> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task EvaluateCompletionAsync(Guid processId, CancellationToken ct)
    {
        _logger.LogInformation(
            "[COMPLETION] Starting evaluation. ProcessId={ProcessId}",
            processId);

        var process = await _uow.Processes.GetByIdAsync(processId, ct);
        if (process == null)
        {
            _logger.LogWarning("[COMPLETION] Process not found. ProcessId={ProcessId}", processId);
            return;
        }
        if (process is null)
        {
            _logger.LogWarning("[COMPLETION] Process not found. ProcessId={ProcessId}", processId);
            return;
        }

        _logger.LogDebug(
            "[COMPLETION] Process loaded. ProcessId={ProcessId} State={State} TokenIdsCount={TokenCount}",
            processId,
            process.State,
            process.TokenIds.Count);

        // اگر پروسس Completed شده اما هنوز توکن دارد، آن را دوباره به Running برگردان
        // این race condition را handle می‌کند: completion evaluation زودتر از موعد اجرا شده
        if (process.State == ProcessState.Completed && process.TokenIds.Count > 0)
        {
            _logger.LogWarning(
                "[COMPLETION] ⚠️ Process is Completed but still has tokens. Resuming process. ProcessId={ProcessId} TokenIdsCount={TokenCount} CompletedAt={CompletedAt}",
                processId,
                process.TokenIds.Count,
                process.CompletedAt);
            
            // Resume the process (change state back to Running)
            process.ResumeFromCompleted();
            
            _logger.LogInformation(
                "[COMPLETION] ✅ Process resumed from Completed to Running. ProcessId={ProcessId}",
                processId);
        }
        
        // فقط پروسس‌های Running را بررسی می‌کنیم
        if (process.State is not ProcessState.Running)
        {
            _logger.LogInformation(
                "[COMPLETION] ⚠️ Process not in Running state. Skipping evaluation. ProcessId={ProcessId} State={State} TokenIdsCount={TokenCount}",
                processId,
                process.State,
                process.TokenIds.Count);
            
            return;
        }

        // همه توکن‌های پروسس را بگیر
        var allTokens = await _uow.Tokens.GetByProcessIdAsync(processId, ct);

        var tokensList = allTokens.ToList();
        _logger.LogDebug(
            "[COMPLETION] Tokens loaded. ProcessId={ProcessId} TotalTokens={Total}",
            processId,
            tokensList.Count);      
        // ✅ Token-Centric Model: Separate executable and trace token analysis
        var liveExecutableTokens = tokensList
            .Where(t => IsLiveExecutableToken(t))
            .ToList();

        var liveTraceTokens = tokensList
            .Where(t => IsLiveTraceToken(t))
            .ToList();

        var terminalTokens = tokensList
            .Where(t => !IsLiveExecutableToken(t) && !IsLiveTraceToken(t))
            .ToList();

        _logger.LogInformation(
            "[COMPLETION] Token analysis. ProcessId={ProcessId} TotalTokens={Total} LiveExecutable={Exec} LiveTrace={Trace} Terminal={Terminal}",
            processId,
            tokensList.Count,
            liveExecutableTokens.Count,
            liveTraceTokens.Count,
            terminalTokens.Count);

        // لاگ جزئیات هر توکن
        foreach (var token in tokensList)
        {
            var isLiveExec = IsLiveExecutableToken(token);
            var isLiveTrace = IsLiveTraceToken(token);
            _logger.LogDebug(
                "[COMPLETION] Token details. ProcessId={ProcessId} TokenId={TokenId} State={State} IsExecutable={Executable} ElementId={ElementId} IsLiveExec={LiveExec} IsLiveTrace={LiveTrace}",
                processId,
                token.Id,
                token.State,
                token.IsExecutable,
                token.CurrentElementId,
                isLiveExec,
                isLiveTrace);
        }

        // لاگ جزئیات توکن‌های زنده
        if (liveExecutableTokens.Count > 0)
        {
            var liveExecDetails = string.Join(", ", liveExecutableTokens.Select(t => $"{t.Id}({t.State})").ToList());
            _logger.LogDebug(
                "[COMPLETION] Live executable tokens details. ProcessId={ProcessId} LiveExecutableTokenIds={TokenIds}",
                processId,
                liveExecDetails);
        }

        if (liveTraceTokens.Count > 0)
        {
            var liveTraceDetails = string.Join(", ", liveTraceTokens.Select(t => $"{t.Id}({t.State})").ToList());
            _logger.LogDebug(
                "[COMPLETION] Live trace tokens details. ProcessId={ProcessId} LiveTraceTokenIds={TokenIds}",
                processId,
                liveTraceDetails);
        }

        // بررسی Open Incidents
        var openIncidents = await _uow.Incidents.GetByProcessIdAsync(processId, ct);
        var openIncidentsList = openIncidents
            .Where(i => i.Status == Domain.ValueObjects.IncidentStatus.Open)
            .ToList();

        _logger.LogDebug(
            "[COMPLETION] Incident analysis. ProcessId={ProcessId} OpenIncidents={OpenIncidents}",
            processId,
            openIncidentsList.Count);

        // لاگ جزئیات Open Incidents
        foreach (var incident in openIncidentsList)
        {
            _logger.LogDebug(
                "[COMPLETION] Open incident details. ProcessId={ProcessId} IncidentId={IncidentId} Type={Type} TokenId={TokenId} ElementId={ElementId} Retries={Retries}",
                processId,
                incident.Id,
                incident.Type,
                incident.TokenId,
                incident.ElementId,
                incident.Retries);
        }

        // ✅ Token-Centric Model: Process completion rules
        // Executable completion: when executable tokens reach zero
        // Trace completion: when all tokens (executable and trace) reach End or are consumed in Join
        // Process completes when:
        // 1. No live executable tokens (executable completion)
        // 2. No live trace tokens (trace completion)
        // 3. No open incidents
        // 4. ProcessState is Running
        if (liveExecutableTokens.Count == 0 && liveTraceTokens.Count == 0 && openIncidentsList.Count == 0)
        {
            _logger.LogInformation(
                "[COMPLETION] ✅ No live executable tokens, no live trace tokens, and no open incidents. Completing process. ProcessId={ProcessId} TotalTokens={Total}",
                processId,
                tokensList.Count);

            process.Complete();

            _logger.LogInformation(
                "[COMPLETION] ✅ Process completed successfully. ProcessId={ProcessId} CompletedAt={CompletedAt}",
                processId,
                process.CompletedAt);
        }
        else
        {
            if (liveExecutableTokens.Count > 0)
            {
                _logger.LogDebug(
                    "[COMPLETION] ⏳ Process still has live executable tokens. Waiting for completion. ProcessId={ProcessId} LiveExecutableCount={LiveExec}",
                    processId,
                    liveExecutableTokens.Count);
            }

            if (liveTraceTokens.Count > 0)
            {
                _logger.LogDebug(
                    "[COMPLETION] ⏳ Process still has live trace tokens. Waiting for trace completion. ProcessId={ProcessId} LiveTraceCount={LiveTrace}",
                    processId,
                    liveTraceTokens.Count);
            }

            if (openIncidentsList.Count > 0)
            {
                _logger.LogWarning(
                    "[COMPLETION] ⚠️ Process has open incidents. Cannot complete. ProcessId={ProcessId} OpenIncidents={OpenIncidents}",
                    processId,
                    openIncidentsList.Count);
            }
        }
    }

    /// <summary>
    /// Determines if an executable token is "live" (should be counted for executable completion evaluation).
    /// 
    /// قانون: Failed token = پروسس تمام نشده
    /// یک Failed token هنوز "زنده" است چون:
    /// - ممکن است retry شود
    /// - ممکن است manual resolve شود
    /// - پروسس نباید complete شود تا زمانی که Failed token resolve شود
    /// 
    /// Live executable tokens are tokens with IsExecutable=true in Created/Active/Waiting/Failed states.
    /// </summary>
    private static bool IsLiveExecutableToken(Token token)
    {
        // فقط توکن‌های executable را حساب می‌کنیم
        if (!token.IsExecutable)
            return false;

        // توکن‌های در حالت‌های زنده (شامل Failed)
        // قانون: Failed token = پروسس تمام نشده
        return token.State is TokenState.Created
            or TokenState.Active
            or TokenState.Waiting
            or TokenState.Failed; // ✅ Failed token هنوز زنده است
    }

    /// <summary>
    /// Determines if a trace token is "live" (should be counted for trace completion evaluation).
    /// 
    /// Trace tokens are non-executable tokens that complete the trace by reaching End or being consumed in Join.
    /// Live trace tokens are tokens with IsExecutable=false in Created/Active/Waiting states.
    /// Trace tokens never fail (they just move through the process).
    /// </summary>
    private static bool IsLiveTraceToken(Token token)
    {
        // فقط توکن‌های trace (non-executable) را حساب می‌کنیم
        if (token.IsExecutable)
            return false;

        // Trace tokens در حالت‌های زنده (Failed نمی‌شوند)
        return token.State is TokenState.Created
            or TokenState.Active
            or TokenState.Waiting;
    }
}

