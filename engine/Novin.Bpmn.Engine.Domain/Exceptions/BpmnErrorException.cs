namespace Novin.Bpmn.Engine.Domain.Exceptions;

/// <summary>
/// Exception برای BPMN Business Errors که باید توسط Error Boundary / Error EventSubprocess handle شوند.
/// این exception نباید به Technical Failure تبدیل شود.
/// </summary>
public sealed class BpmnErrorException : Exception
{
    /// <summary>
    /// Error code از BPMN model
    /// </summary>
    public string Code { get; }

    public BpmnErrorException(string code, string message)
        : base(message)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Error code cannot be null or empty", nameof(code));
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Error message cannot be null or empty", nameof(message));

        Code = code;
    }

    public BpmnErrorException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Error code cannot be null or empty", nameof(code));
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Error message cannot be null or empty", nameof(message));

        Code = code;
    }
}

