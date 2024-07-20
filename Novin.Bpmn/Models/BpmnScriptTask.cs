namespace Novin.Bpmn.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("scriptTask", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public partial class BpmnScriptTask : BpmnTask
{
    private System.Xml.XmlNode scriptField;

    private string scriptFormatField;

    /// <remarks/>
    public System.Xml.XmlNode script
    {
        get { return scriptField; }
        set { scriptField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public string scriptFormat
    {
        get { return scriptFormatField; }
        set { scriptFormatField = value; }
    }
}