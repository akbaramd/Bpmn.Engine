namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("correlationPropertyRetrievalExpression",
    Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public partial class BpmnCorrelationPropertyRetrievalExpression : BpmnBaseElement
{
    private BpmnFormalExpression messagePathField;

    private System.Xml.XmlQualifiedName messageRefField;

    /// <remarks/>
    public BpmnFormalExpression messagePath
    {
        get { return messagePathField; }
        set { messagePathField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public System.Xml.XmlQualifiedName messageRef
    {
        get { return messageRefField; }
        set { messageRefField = value; }
    }
}