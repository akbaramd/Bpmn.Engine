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
[XmlRoot("textAnnotation", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public class BpmnTextAnnotation : BpmnArtifact
{
    private XmlNode textField;

    private string textFormatField;

    public BpmnTextAnnotation()
    {
        textFormatField = "text/plain";
    }

    /// <remarks/>
    public XmlNode text
    {
        get { return textField; }
        set { textField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    [DefaultValue("text/plain")]
    public string textFormat
    {
        get { return textFormatField; }
        set { textFormatField = value; }
    }
}