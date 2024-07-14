using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml;
using System.Xml.Serialization;

namespace Novin.Bpmn.Test.Models;

/// <remarks />
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlRoot("participantAssociation", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public class BpmnParticipantAssociation : BpmnBaseElement
{
    private XmlQualifiedName innerParticipantRefField;

    private XmlQualifiedName outerParticipantRefField;

    /// <remarks />
    public XmlQualifiedName innerParticipantRef
    {
        get => innerParticipantRefField;
        set => innerParticipantRefField = value;
    }

    /// <remarks />
    public XmlQualifiedName outerParticipantRef
    {
        get => outerParticipantRefField;
        set => outerParticipantRefField = value;
    }
}