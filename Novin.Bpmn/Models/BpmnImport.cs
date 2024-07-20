namespace Novin.Bpmn.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("import", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public partial class BpmnImport
{
    private string namespaceField;

    private string locationField;

    private string importTypeField;

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute(DataType = "anyURI")]
    public string @namespace
    {
        get { return namespaceField; }
        set { namespaceField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public string location
    {
        get { return locationField; }
        set { locationField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute(DataType = "anyURI")]
    public string importType
    {
        get { return importTypeField; }
        set { importTypeField = value; }
    }
}