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
[XmlRoot("globalChoreographyTask",
    Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public class BpmnGlobalChoreographyTask : BpmnChoreography
{
    private XmlQualifiedName initiatingParticipantRefField;

    /// <remarks/>
    [XmlAttribute]
    public XmlQualifiedName initiatingParticipantRef
    {
        get { return initiatingParticipantRefField; }
        set { initiatingParticipantRefField = value; }
    }
}