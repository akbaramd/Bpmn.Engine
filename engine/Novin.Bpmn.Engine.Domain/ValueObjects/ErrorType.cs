namespace Novin.Bpmn.Engine.Domain.ValueObjects;

/// <summary>
/// نوع خطا در BPMN Engine
/// </summary>
public enum ErrorType
{
    /// <summary>
    /// BPMN Error: خطای business که مدل می‌خواهد آن را با Error Boundary / Error EventSubprocess بگیرد.
    /// این «خرابی سیستم» نیست و باید توسط مدل BPMN handle شود.
    /// </summary>
    BpmnError,

    /// <summary>
    /// Technical Failure: خطای تکنیکی/سیستمی (اسکریپت، DB، HTTP، NullRef، ...)
    /// این باید به Incident تبدیل شود و قابل Retry/Manual resolve باشد.
    /// </summary>
    TechnicalFailure
}

