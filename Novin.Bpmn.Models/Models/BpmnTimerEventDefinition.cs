using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Serialization;

namespace Novin.Bpmn.Models;

/// <summary>
/// Represents a Timer Event Definition in a BPMN process.
/// </summary>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlRoot("timerEventDefinition", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public class BpmnTimerEventDefinition : BpmnEventDefinition
{
    private BpmnExpression timeCycleField;
    private BpmnExpression timeDateField;
    private BpmnExpression timeDurationField;

    /// <summary>
    /// Represents the `<bpmn:timeCycle>` element in the BPMN XML.
    /// Defines the repetition of a timer.
    /// </summary>
    [XmlElement("timeCycle", typeof(BpmnExpression))]
    public BpmnExpression TimeCycle
    {
        get => timeCycleField;
        set => timeCycleField = value;
    }

    /// <summary>
    /// Represents the `<bpmn:timeDate>` element in the BPMN XML.
    /// Specifies a specific date and time for the timer.
    /// </summary>
    [XmlElement("timeDate", typeof(BpmnExpression))]
    public BpmnExpression TimeDate
    {
        get => timeDateField;
        set => timeDateField = value;
    }

    /// <summary>
    /// Represents the `<bpmn:timeDuration>` element in the BPMN XML.
    /// Defines the duration after which the timer triggers.
    /// </summary>
    [XmlElement("timeDuration", typeof(BpmnExpression))]
    public BpmnExpression TimeDuration
    {
        get => timeDurationField;
        set => timeDurationField = value;
    }

    /// <summary>
    /// Gets the value of the `<bpmn:timeDuration>` element, if available.
    /// </summary>
    public string GetTimeDuration()
    {
        return TimeDuration?.Text?.FirstOrDefault() ?? string.Empty;
    }

    /// <summary>
    /// Gets the value of the `<bpmn:timeCycle>` element, if available.
    /// </summary>
    public string GetTimeCycle()
    {
        return TimeCycle?.Text?.FirstOrDefault() ?? string.Empty;
    }

    /// <summary>
    /// Gets the value of the `<bpmn:timeDate>` element, if available.
    /// </summary>
    public string GetTimeDate()
    {
        return TimeDate?.Text?.FirstOrDefault() ?? string.Empty;
    }

    /// <summary>
    /// Gets the type of the timer (timeCycle, timeDate, or timeDuration) and its corresponding value.
    /// </summary>
    /// <returns>A tuple containing the timer type and its value.</returns>
    public (string TimerType, string TimerValue) GetTimerTypeAndValue()
    {
        if (TimeCycle != null && !string.IsNullOrWhiteSpace(GetTimeCycle()))
            return ("timeCycle", GetTimeCycle());

        if (TimeDate != null && !string.IsNullOrWhiteSpace(GetTimeDate()))
            return ("timeDate", GetTimeDate());

        if (TimeDuration != null && !string.IsNullOrWhiteSpace(GetTimeDuration()))
            return ("timeDuration", GetTimeDuration());

        return (string.Empty, string.Empty); // No timer type defined
    }
}
