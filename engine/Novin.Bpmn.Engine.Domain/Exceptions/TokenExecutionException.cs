namespace Novin.Bpmn.Engine.Domain.Exceptions;

/// <summary>
/// Exception که در طول اجرای یک token رخ می‌دهد.
/// این exception برای wrap کردن exceptions در لایه‌های مختلف استفاده می‌شود
/// تا در لایه بالاتر (Orchestrator) تصمیم‌گیری شود.
/// </summary>
public sealed class TokenExecutionException : Exception
{
    public Guid TokenId { get; }
    public string ElementId { get; }
    public Guid ProcessId { get; }

    public TokenExecutionException(
        Guid processId,
        Guid tokenId,
        string elementId,
        Exception innerException)
        : base($"Token execution failed. ProcessId={processId}, TokenId={tokenId}, ElementId={elementId}", innerException)
    {
        if (string.IsNullOrWhiteSpace(elementId))
            throw new ArgumentException("ElementId cannot be null or empty", nameof(elementId));

        ProcessId = processId;
        TokenId = tokenId;
        ElementId = elementId;
    }

    public TokenExecutionException(
        Guid processId,
        Guid tokenId,
        string elementId,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        if (string.IsNullOrWhiteSpace(elementId))
            throw new ArgumentException("ElementId cannot be null or empty", nameof(elementId));

        ProcessId = processId;
        TokenId = tokenId;
        ElementId = elementId;
    }
}

