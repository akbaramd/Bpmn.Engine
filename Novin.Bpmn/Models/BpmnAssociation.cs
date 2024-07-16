namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("association", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public partial class BpmnAssociation : BpmnArtifact
{
    private System.Xml.XmlQualifiedName sourceRefField;

    private System.Xml.XmlQualifiedName targetRefField;

    private BpmnAssociationDirection associationDirectionField;

    public BpmnAssociation()
    {
        associationDirectionField = BpmnAssociationDirection.None;
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public System.Xml.XmlQualifiedName sourceRef
    {
        get { return sourceRefField; }
        set { sourceRefField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public System.Xml.XmlQualifiedName targetRef
    {
        get { return targetRefField; }
        set { targetRefField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    [System.ComponentModel.DefaultValueAttribute(BpmnAssociationDirection.None)]
    public BpmnAssociationDirection associationDirection
    {
        get { return associationDirectionField; }
        set { associationDirectionField = value; }
    }
}