using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml;
using System.Xml.Serialization;

namespace Novin.Bpmn.Models;

/// <remarks />
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlRoot("choreographyTask", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public class BpmnChoreographyTask : BpmnChoreographyActivity
{
    private XmlQualifiedName[] messageFlowRefField;

    /// <remarks />
    [XmlElement("messageFlowRef")]
    public XmlQualifiedName[] messageFlowRef
    {
        get => messageFlowRefField;
        set => messageFlowRefField = value;
    }
}