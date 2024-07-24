using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Serialization;

namespace Novin.Bpmn.Models;

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlRoot("transaction", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public class BpmnTransaction : BpmnSubProcess
{
    private string methodField;

    public BpmnTransaction()
    {
        methodField = "##Compensate";
    }

    /// <remarks/>
    [XmlAttribute]
    [DefaultValue("##Compensate")]
    public string method
    {
        get { return methodField; }
        set { methodField = value; }
    }
}