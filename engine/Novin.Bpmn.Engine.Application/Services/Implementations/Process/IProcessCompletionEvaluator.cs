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