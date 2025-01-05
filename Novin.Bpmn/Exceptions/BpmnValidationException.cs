using System.Runtime.Serialization;

namespace Novin.Bpmn.Exceptions;

public class BpmnValidationException : Exception
{
    public BpmnValidationException()
    {
    }

    protected BpmnValidationException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }

    public BpmnValidationException(string? message) : base(message)
    {
    }

    public BpmnValidationException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}