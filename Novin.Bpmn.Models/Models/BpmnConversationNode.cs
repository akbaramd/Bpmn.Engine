using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml;
using System.Xml.Serialization;

namespace Novin.Bpmn.Models;

/// <remarks/>
[XmlInclude(typeof(BpmnSubConversation))]
[XmlInclude(typeof(BpmnConversation))]
[XmlInclude(typeof(BpmnCallConversation))]
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlRoot("conversationNode",
    Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public abstract class BpmnConversationNode : BpmnBaseElement
{
    private XmlQualifiedName[] participantRefField;

    private XmlQualifiedName[] messageFlowRefField;

    private BpmnCorrelationKey[] correlationKeyField;

    private string nameField;

    /// <remarks/>
    [XmlElement("participantRef")]
    public XmlQualifiedName[] participantRef
    {
        get { return participantRefField; }
        set { participantRefField = value; }
    }

    /// <remarks/>
    [XmlElement("messageFlowRef")]
    public XmlQualifiedName[] messageFlowRef
    {
        get { return messageFlowRefField; }
        set { messageFlowRefField = value; }
    }

    /// <remarks/>
    [XmlElement("correlationKey")]
    public BpmnCorrelationKey[] correlationKey
    {
        get { return correlationKeyField; }
        set { correlationKeyField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string name
    {
        get { return nameField; }
        set { nameField = value; }
    }
}