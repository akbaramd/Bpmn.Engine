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
[XmlRoot("signalEventDefinition",
    Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public class BpmnSignalEventDefinition : BpmnEventDefinition
{
    private XmlQualifiedName signalRefField;

    /// <remarks/>
    [XmlAttribute]
    public XmlQualifiedName signalRef
    {
        get { return signalRefField; }
        set { signalRefField = value; }
    }
}