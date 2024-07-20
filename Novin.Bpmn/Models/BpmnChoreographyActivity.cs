using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml;
using System.Xml.Serialization;

namespace Novin.Bpmn.Models;

/// <remarks />
[XmlInclude(typeof(BpmnSubChoreography))]
[XmlInclude(typeof(BpmnChoreographyTask))]
[XmlInclude(typeof(BpmnCallChoreography))]
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlRoot("choreographyActivity", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public abstract class BpmnChoreographyActivity : BpmnFlowNode
{
    private BpmnCorrelationKey[] correlationKeyField;

    private XmlQualifiedName initiatingParticipantRefField;

    private BpmnChoreographyLoopType loopTypeField;

    private XmlQualifiedName[] participantRefField;

    public BpmnChoreographyActivity()
    {
        loopTypeField = BpmnChoreographyLoopType.None;
    }

    /// <remarks />
    [XmlElement("participantRef")]
    public XmlQualifiedName[] participantRef
    {
        get => participantRefField;
        set => participantRefField = value;
    }

    /// <remarks />
    [XmlElement("correlationKey")]
    public BpmnCorrelationKey[] correlationKey
    {
        get => correlationKeyField;
        set => correlationKeyField = value;
    }

    /// <remarks />
    [XmlAttribute]
    public XmlQualifiedName initiatingParticipantRef
    {
        get => initiatingParticipantRefField;
        set => initiatingParticipantRefField = value;
    }

    /// <remarks />
    [XmlAttribute]
    [DefaultValue(BpmnChoreographyLoopType.None)]
    public BpmnChoreographyLoopType loopType
    {
        get => loopTypeField;
        set => loopTypeField = value;
    }
}