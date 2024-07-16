namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("extension", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public partial class BpmnExtension
{
    private BpmnDocumentation[] documentationField;

    private System.Xml.XmlQualifiedName definitionField;

    private bool mustUnderstandField;

    public BpmnExtension()
    {
        mustUnderstandField = false;
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("documentation")]
    public BpmnDocumentation[] documentation
    {
        get { return documentationField; }
        set { documentationField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public System.Xml.XmlQualifiedName definition
    {
        get { return definitionField; }
        set { definitionField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    [System.ComponentModel.DefaultValueAttribute(false)]
    public bool mustUnderstand
    {
        get { return mustUnderstandField; }
        set { mustUnderstandField = value; }
    }
}