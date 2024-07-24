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
[XmlRoot("callConversation",
    Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public class BpmnCallConversation : BpmnConversationNode
{
    private BpmnParticipantAssociation[] participantAssociationField;

    private XmlQualifiedName calledCollaborationRefField;

    /// <remarks/>
    [XmlElement("participantAssociation")]
    public BpmnParticipantAssociation[] participantAssociation
    {
        get { return participantAssociationField; }
        set { participantAssociationField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public XmlQualifiedName calledCollaborationRef
    {
        get { return calledCollaborationRefField; }
        set { calledCollaborationRefField = value; }
    }
}