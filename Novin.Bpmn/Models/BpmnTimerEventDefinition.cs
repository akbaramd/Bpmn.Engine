using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Serialization;

namespace Novin.Bpmn.Models;

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlRoot("timerEventDefinition",
    Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public class BpmnTimerEventDefinition : BpmnEventDefinition
{
    private BpmnExpression itemField;

    private ItemChoiceType itemElementNameField;

    /// <remarks/>
    [XmlElement("timeCycle", typeof(BpmnExpression))]
    [XmlElement("timeDate", typeof(BpmnExpression))]
    [XmlElement("timeDuration", typeof(BpmnExpression))]
    [XmlChoiceIdentifier("ItemElementName")]
    public BpmnExpression Item
    {
        get { return itemField; }
        set { itemField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public ItemChoiceType ItemElementName
    {
        get { return itemElementNameField; }
        set { itemElementNameField = value; }
    }
}