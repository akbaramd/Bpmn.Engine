using Novin.Bpmn.Engine.Application;

namespace Novin.Bpmn.Engine.Domain.Exceptions;


/// <summary>
/// Unified exception from ScriptTaskExecutor to orchestrator/handler.
/// Handler maps this to node.Fail(message, kind) and (if you want) token.Fail(...).
/// </summary>
public sealed class ScriptTaskExecutionException : Exception
{
    public Guid ProcessId { get; }
    public Guid TokenId { get; }
    public string TaskId { get; }
    public EngineErrorKind Kind { get; }

    /// <summary>
    /// Optional BPMN ErrorCode when Kind == BpmnError
    /// </summary>
    public string? BpmnErrorCode { get; }

    public ScriptTaskExecutionException(
        Guid processId,
        Guid tokenId,
        string taskId,
        string message,
        EngineErrorKind kind,
        string? bpmnErrorCode = null,
        Exception? inner = null)
        : base(message, inner)
    {
        ProcessId = processId;
        TokenId = tokenId;
        TaskId = taskId;
        Kind = kind;
        BpmnErrorCode = bpmnErrorCode;
    }

    public override string ToString()
        => $"[{Kind}] ScriptTaskExecutionException TaskId={TaskId} P={ProcessId} T={TokenId} Code={BpmnErrorCode} :: {Message}";
}