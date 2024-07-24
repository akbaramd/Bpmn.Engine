using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml;
using System.Xml.Serialization;

namespace Novin.Bpmn.Models;

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlRoot("scriptTask", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public class BpmnScriptTask : BpmnTask
{
    private XmlNode scriptField;

    private string scriptFormatField;

    /// <remarks/>
    public XmlNode script
    {
        get { return scriptField; }
        set { scriptField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string scriptFormat
    {
        get { return scriptFormatField; }
        set { scriptFormatField = value; }
    }
}