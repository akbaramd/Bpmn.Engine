using MediatR;
using Microsoft.Extensions.Logging;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Application.Services;
using Novin.Bpmn.Engine.Domain.Events;
using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Application.EventHandlers;

public sealed class TokenCompletedEventHandler : INotificationHandler<TokenCompletedEvent>
{
    private readonly IProcessCompletionEvaluator _evaluator;
    private readonly IProcessExecutionRecorder _recorder;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<TokenCompletedEventHandler> _logger;

    public TokenCompletedEventHandler(
        IProcessCompletionEvaluator evaluator,
        IProcessExecutionRecorder recorder,
        IUnitOfWork uow,
        ILogger<TokenCompletedEventHandler> logger)
    {
        _evaluator = evaluator;
        _recorder = recorder;
        _uow = uow;
        _logger = logger;
    }

    public async System.Threading.Tasks.Task Handle(TokenCompletedEvent e, CancellationToken ct)
    {
        _logger.LogInformation("[TOKEN-COMPLETED] TokenId={TokenId} ProcessId={ProcessId} ElementId={ElementId} Exec={Exec} Scope={Scope}",
            e.TokenId, e.ProcessId, e.ElementId, e.IsExecutable, e.ScopeId);

        // 1) Best-effort record (اگر دوست داری)
        try
        {
            var process = await _uow.Processes.GetByIdAsync(e.ProcessId, ct);
            var token = await _uow.Tokens.GetByIdAsync(e.TokenId, ct);
            if (process != null && token != null)
            {
                await _recorder.RecordNodeExecutionAsync(process, token, e.ElementId, arrivedViaFlowId: token.ArrivedViaFlowId, ct: ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[TOKEN-COMPLETED] Recorder failed (best-effort).");
        }

        // 2) Evaluate completion
        await _evaluator.EvaluateCompletionAsync(e.ProcessId, ct);

        // ✅ هیچ navigation اینجا نداریم
    }
}