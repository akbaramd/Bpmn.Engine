namespace Novin.Bpmn.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("linkEventDefinition",
    Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public partial class BpmnLinkEventDefinition : BpmnEventDefinition
{
    private System.Xml.XmlQualifiedName[] sourceField;

    private System.Xml.XmlQualifiedName targetField;

    private string nameField;

    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("source")]
    public System.Xml.XmlQualifiedName[] source
    {
        get { return sourceField; }
        set { sourceField = value; }
    }

    /// <remarks/>
    public System.Xml.XmlQualifiedName target
    {
        get { return targetField; }
        set { targetField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public string name
    {
        get { return nameField; }
        set { nameField = value; }
    }
}