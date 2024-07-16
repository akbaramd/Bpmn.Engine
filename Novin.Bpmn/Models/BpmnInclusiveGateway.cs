namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("inclusiveGateway",
    Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public partial class BpmnInclusiveGateway : BpmnGateway
{
    private string defaultField;

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute(DataType = "IDREF")]
    public string @default
    {
        get { return defaultField; }
        set { defaultField = value; }
    }
}