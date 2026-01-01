namespace Novin.Bpmn.Engine.Domain.ValueObjects;

/// <summary>
/// Type of BPMN timer expression
/// </summary>
public enum TimerType
{
    /// <summary>
    /// timeDate: fires at a specific date/time (one-shot)
    /// </summary>
    TimeDate,

    /// <summary>
    /// timeDuration: fires after a duration from activity start (one-shot)
    /// </summary>
    TimeDuration,

    /// <summary>
    /// timeCycle: fires repeatedly at intervals
    /// </summary>
    TimeCycle
}

