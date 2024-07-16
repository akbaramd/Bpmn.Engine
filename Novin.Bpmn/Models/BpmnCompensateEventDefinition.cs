namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("compensateEventDefinition",
    Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public partial class BpmnCompensateEventDefinition : BpmnEventDefinition
{
    private bool waitForCompletionField;

    private bool waitForCompletionFieldSpecified;

    private System.Xml.XmlQualifiedName activityRefField;

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public bool waitForCompletion
    {
        get { return waitForCompletionField; }
        set { waitForCompletionField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlIgnoreAttribute()]
    public bool waitForCompletionSpecified
    {
        get { return waitForCompletionFieldSpecified; }
        set { waitForCompletionFieldSpecified = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public System.Xml.XmlQualifiedName activityRef
    {
        get { return activityRefField; }
        set { activityRefField = value; }
    }
}