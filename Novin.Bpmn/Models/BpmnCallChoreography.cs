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
[XmlRoot("callChoreography", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public class BpmnCallChoreography : BpmnChoreographyActivity
{
    private XmlQualifiedName calledChoreographyRefField;

    private BpmnParticipantAssociation[] participantAssociationField;

    /// <remarks />
    [XmlElement("participantAssociation")]
    public BpmnParticipantAssociation[] participantAssociation
    {
        get => participantAssociationField;
        set => participantAssociationField = value;
    }

    /// <remarks />
    [XmlAttribute]
    public XmlQualifiedName calledChoreographyRef
    {
        get => calledChoreographyRefField;
        set => calledChoreographyRefField = value;
    }
}