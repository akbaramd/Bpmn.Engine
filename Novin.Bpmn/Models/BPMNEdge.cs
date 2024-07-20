namespace Novin.Bpmn.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.omg.org/spec/BPMN/20100524/DI")]
[System.Xml.Serialization.XmlRootAttribute(Namespace = "http://www.omg.org/spec/BPMN/20100524/DI", IsNullable = false)]
public partial class BPMNEdge : LabeledEdge
{
    private BPMNLabel bPMNLabelField;

    private System.Xml.XmlQualifiedName bpmnElementField;

    private System.Xml.XmlQualifiedName sourceElementField;

    private System.Xml.XmlQualifiedName targetElementField;

    private MessageVisibleKind messageVisibleKindField;

    private bool messageVisibleKindFieldSpecified;

    /// <remarks/>
    public BPMNLabel BPMNLabel
    {
        get { return bPMNLabelField; }
        set { bPMNLabelField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public System.Xml.XmlQualifiedName bpmnElement
    {
        get { return bpmnElementField; }
        set { bpmnElementField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public System.Xml.XmlQualifiedName sourceElement
    {
        get { return sourceElementField; }
        set { sourceElementField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public System.Xml.XmlQualifiedName targetElement
    {
        get { return targetElementField; }
        set { targetElementField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public MessageVisibleKind messageVisibleKind
    {
        get { return messageVisibleKindField; }
        set { messageVisibleKindField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlIgnoreAttribute()]
    public bool messageVisibleKindSpecified
    {
        get { return messageVisibleKindFieldSpecified; }
        set { messageVisibleKindFieldSpecified = value; }
    }
}