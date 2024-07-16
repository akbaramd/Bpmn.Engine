namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("adHocSubProcess", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public partial class BpmnAdHocSubProcess : BpmnSubProcess
{
    private BpmnExpression completionConditionField;

    private bool cancelRemainingInstancesField;

    private BpmnAdHocOrdering orderingField;

    private bool orderingFieldSpecified;

    public BpmnAdHocSubProcess()
    {
        cancelRemainingInstancesField = true;
    }

    /// <remarks/>
    public BpmnExpression completionCondition
    {
        get { return completionConditionField; }
        set { completionConditionField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    [System.ComponentModel.DefaultValueAttribute(true)]
    public bool cancelRemainingInstances
    {
        get { return cancelRemainingInstancesField; }
        set { cancelRemainingInstancesField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public BpmnAdHocOrdering ordering
    {
        get { return orderingField; }
        set { orderingField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlIgnoreAttribute()]
    public bool orderingSpecified
    {
        get { return orderingFieldSpecified; }
        set { orderingFieldSpecified = value; }
    }
}