using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.ValueObjects;

namespace Novin.Bpmn.Engine.Application.Services;

/// <summary>
/// Evaluates process completion based on BPMN2 semantics:
/// - Process is completed when no live executable tokens remain
/// - Live tokens = tokens with state Created/Active/Waiting AND IsExecutable == true
/// </summary>
public interface IProcessCompletionEvaluator
{
    Task EvaluateCompletionAsync(Guid processId, CancellationToken ct);
}

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

        // فقط پروسس‌های Running را بررسی می‌کنیم
        if (process.State is not ProcessState.Running)
        {
            _logger.LogDebug(
                "[COMPLETION] Process not in Running state. Skipping evaluation. ProcessId={ProcessId} State={State}",
                processId,
                process.State);
            return;
        }

        // همه توکن‌های پروسس را بگیر
        var allTokens = await _uow.Tokens.GetByProcessIdAsync(processId, ct);

        var tokensList = allTokens.ToList();
        _logger.LogDebug(
            "[COMPLETION] Tokens loaded. ProcessId={ProcessId} TotalTokens={Total}",
            processId,
            tokensList.Count);      
        // شمارش توکن‌های زنده (Live tokens)
        var liveTokens = tokensList
            .Where(t => IsLiveToken(t))
            .ToList();

        var terminalTokens = tokensList
            .Where(t => !IsLiveToken(t))
            .ToList();

        _logger.LogInformation(
            "[COMPLETION] Token analysis. ProcessId={ProcessId} TotalTokens={Total} LiveTokens={Live} TerminalTokens={Terminal}",
            processId,
            tokensList.Count,
            liveTokens.Count,
            terminalTokens.Count);

        // لاگ جزئیات هر توکن
        foreach (var token in tokensList)
        {
            _logger.LogDebug(
                "[COMPLETION] Token details. ProcessId={ProcessId} TokenId={TokenId} State={State} IsExecutable={Executable} ElementId={ElementId} IsLive={IsLive}",
                processId,
                token.Id,
                token.State,
                token.IsExecutable,
                token.CurrentElementId,
                IsLiveToken(token));
        }

        // لاگ جزئیات توکن‌های زنده
        if (liveTokens.Count > 0)
        {
            var liveTokenDetails = string.Join(", ", liveTokens.Select(t => $"{t.Id}({t.State})").ToList());
            _logger.LogDebug(
                "[COMPLETION] Live tokens details. ProcessId={ProcessId} LiveTokenIds={TokenIds}",
                processId,
                liveTokenDetails);
        }

        // اگر هیچ توکن زنده‌ای باقی نمانده، پروسس را complete کن
        if (liveTokens.Count == 0)
        {
            _logger.LogWarning(
                "[COMPLETION] ✅ No live tokens remaining. Completing process. ProcessId={ProcessId} TotalTokens={Total}",
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
            _logger.LogDebug(
                "[COMPLETION] ⏳ Process still has live tokens. Waiting for completion. ProcessId={ProcessId} LiveCount={Live}",
                processId,
                liveTokens.Count);
        }
    }

    /// <summary>
    /// Determines if a token is "live" (should be counted for completion evaluation).
    /// Live tokens are executable tokens in Created/Active/Waiting states.
    /// </summary>
    private static bool IsLiveToken(Token token)
    {
        // فقط توکن‌های executable را حساب می‌کنیم (bypass tokens را نادیده می‌گیریم)
        if (!token.IsExecutable)
            return false;

        // توکن‌های در حالت‌های زنده
        return token.State is TokenState.Created
            or TokenState.Active
            or TokenState.Waiting;
    }
}

