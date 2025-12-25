using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Entities;
using Novin.Bpmn.Engine.Domain.Exceptions;
using Novin.Bpmn.Engine.Domain.ValueObjects;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

/// <summary>
/// Decorator که Variable Mapping را به صورت خودکار قبل و بعد از اجرای هر BPMN element اعمال می‌کند.
/// این کلاس از Decorator Pattern برای enforce کردن lifecycle استفاده می‌کند:
/// 1. ApplyInputs (process → token locals)
/// 2. Execute Business Logic (via inner handler)
/// 3. ApplyOutputs (token locals → process)
/// </summary>
public sealed class VariableMappingElementHandlerDecorator : IBpmnElementHandler
{
    private readonly IBpmnElementHandler _innerHandler;
    private readonly IVariableMappingService _mappingService;
    private readonly ILogger<VariableMappingElementHandlerDecorator> _logger;

    public VariableMappingElementHandlerDecorator(
        IBpmnElementHandler innerHandler,
        IVariableMappingService mappingService,
        ILogger<VariableMappingElementHandlerDecorator> logger)
    {
        _innerHandler = innerHandler ?? throw new ArgumentNullException(nameof(innerHandler));
        _mappingService = mappingService ?? throw new ArgumentNullException(nameof(mappingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool CanHandle(BpmnFlowElement element) => _innerHandler.CanHandle(element);

    public async Task HandleAsync(
        Process process,
        Token token,
        BpmnFlowElement element,
        BpmnRuntimeContext ctx,
        CancellationToken ct)
    {
        var elementId = element.id ?? "unknown";
        var elementType = element.GetType().Name;
        var handlerType = _innerHandler.GetType().Name;

        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["ProcessId"] = process.Id,
            ["TokenId"] = token.Id,
            ["ElementId"] = elementId,
            ["ElementType"] = elementType,
            ["HandlerType"] = handlerType,
            ["TokenState"] = token.State.ToString(),
            ["IsExecutable"] = token.IsExecutable
        }))
        {
            // Guards: اگر token قبلاً failed/terminated شده، mapping نمی‌زنیم
            if (token.State is TokenState.Failed or TokenState.Terminated)
            {
                _logger.LogDebug(
                    "[MAPPING-DECORATOR] Skipping mapping for terminated/failed token. State={State}",
                    token.State);

                await _innerHandler.HandleAsync(process, token, element, ctx, ct);
                return;
            }

            try
            {
                // ─────────────────────────────────────────────────────────
                // Phase 0: Reset Token Locals (قانون: هر نود فقط locals خودش را دارد)
                // ─────────────────────────────────────────────────────────
                var varsBeforeReset = token.Variables.Count;
                token.ClearLocalVariables();
                
                _logger.LogDebug(
                    "[MAPPING-DECORATOR] Phase 0: Reset token locals. Cleared {Count} variables",
                    varsBeforeReset);

                // ─────────────────────────────────────────────────────────
                // Phase 1: ApplyInputs (process vars → token locals)
                // ─────────────────────────────────────────────────────────
                _logger.LogDebug("[MAPPING-DECORATOR] Phase 1: ApplyInputs START");

                _mappingService.ApplyInputs(process, token, element, ctx);

                _logger.LogDebug(
                    "[MAPPING-DECORATOR] Phase 1: ApplyInputs DONE. TokenVarsCount={Count} TokenVarsKeys={Keys}",
                    token.Variables.Count,
                    string.Join(",", token.Variables.Keys.Take(10)));

                // ─────────────────────────────────────────────────────────
                // Phase 2: Execute Business Logic
                // ─────────────────────────────────────────────────────────
                _logger.LogDebug("[MAPPING-DECORATOR] Phase 2: Execute business logic via {Handler}", handlerType);

                await _innerHandler.HandleAsync(process, token, element, ctx, ct);

                _logger.LogDebug(
                    "[MAPPING-DECORATOR] Phase 2: Business logic DONE. TokenState={State}",
                    token.State);

                // ─────────────────────────────────────────────────────────
                // Phase 3: ApplyOutputs (token locals → process vars)
                // ─────────────────────────────────────────────────────────
                // فقط اگر token هنوز executable و موفق است
                if (token.State is TokenState.Failed or TokenState.Terminated)
                {
                    _logger.LogWarning(
                        "[MAPPING-DECORATOR] Phase 3: Skipping ApplyOutputs due to state={State}",
                        token.State);
                    return;
                }

                _logger.LogDebug("[MAPPING-DECORATOR] Phase 3: ApplyOutputs START");

                _mappingService.ApplyOutputs(process, token, element, ctx);

                _logger.LogDebug(
                    "[MAPPING-DECORATOR] Phase 3: ApplyOutputs DONE. ProcessVarsCount={Count} ProcessVarsKeys={Keys}",
                    process.Variables.Count,
                    string.Join(",", process.Variables.Keys.Take(10)));
            }
            catch (BpmnErrorException)
            {
                // BPMN Error را بدون تغییر propagate می‌کنیم
                // این باید توسط Error Boundary / Error EventSubprocess handle شود
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "[MAPPING-DECORATOR] Unhandled exception during lifecycle. ElementId={ElementId} Handler={Handler}",
                    elementId,
                    handlerType);

                // Decorator نباید token.Fail() را صدا بزند چون transaction rollback می‌شود
                // به جای آن، exception را wrap می‌کنیم تا در لایه بالاتر (Orchestrator) تصمیم‌گیری شود
                throw new TokenExecutionException(
                    process.Id,
                    token.Id,
                    elementId,
                    ex);
            }
        }
    }
}

