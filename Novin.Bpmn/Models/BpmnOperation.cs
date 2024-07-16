namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("operation", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public partial class BpmnOperation : BpmnBaseElement
{
    private System.Xml.XmlQualifiedName inMessageRefField;

    private System.Xml.XmlQualifiedName outMessageRefField;

    private System.Xml.XmlQualifiedName[] errorRefField;

    private string nameField;

    private System.Xml.XmlQualifiedName implementationRefField;

    /// <remarks/>
    public System.Xml.XmlQualifiedName inMessageRef
    {
        get { return inMessageRefField; }
        set { inMessageRefField = value; }
    }

    /// <remarks/>
    public System.Xml.XmlQualifiedName outMessageRef
    {
        get { return outMessageRefField; }
        set { outMessageRefField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("errorRef")]
    public System.Xml.XmlQualifiedName[] errorRef
    {
        get { return errorRefField; }
        set { errorRefField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public string name
    {
        get { return nameField; }
        set { nameField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public System.Xml.XmlQualifiedName implementationRef
    {
        get { return implementationRefField; }
        set { implementationRefField = value; }
    }
}