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
[XmlRoot("escalationEventDefinition",
    Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public class BpmnEscalationEventDefinition : BpmnEventDefinition
{
    private XmlQualifiedName escalationRefField;

    /// <remarks/>
    [XmlAttribute]
    public XmlQualifiedName escalationRef
    {
        get { return escalationRefField; }
        set { escalationRefField = value; }
    }
}