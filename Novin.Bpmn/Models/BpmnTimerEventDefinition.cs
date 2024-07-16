namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("timerEventDefinition",
    Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public partial class BpmnTimerEventDefinition : BpmnEventDefinition
{
    private BpmnExpression itemField;

    private ItemChoiceType itemElementNameField;

    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("timeCycle", typeof(BpmnExpression))]
    [System.Xml.Serialization.XmlElementAttribute("timeDate", typeof(BpmnExpression))]
    [System.Xml.Serialization.XmlElementAttribute("timeDuration", typeof(BpmnExpression))]
    [System.Xml.Serialization.XmlChoiceIdentifierAttribute("ItemElementName")]
    public BpmnExpression Item
    {
        get { return itemField; }
        set { itemField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlIgnoreAttribute()]
    public ItemChoiceType ItemElementName
    {
        get { return itemElementNameField; }
        set { itemElementNameField = value; }
    }
}