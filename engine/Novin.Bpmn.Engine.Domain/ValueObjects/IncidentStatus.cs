namespace Novin.Bpmn.Engine.Domain.ValueObjects;

/// <summary>
/// وضعیت یک Incident
/// </summary>
public enum IncidentStatus
{
    /// <summary>
    /// Incident باز است و نیاز به حل دارد
    /// </summary>
    Open,

    /// <summary>
    /// Incident حل شده است (manual resolve یا retry موفق)
    /// </summary>
    Resolved
}

