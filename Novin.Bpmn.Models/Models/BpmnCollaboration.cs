using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml;
using System.Xml.Serialization;

namespace Novin.Bpmn.Models;

/// <remarks/>
[XmlInclude(typeof(BpmnGlobalConversation))]
[XmlInclude(typeof(BpmnChoreography))]
[XmlInclude(typeof(BpmnGlobalChoreographyTask))]
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlRoot("collaboration", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public class BpmnCollaboration : BpmnRootElement
{
    private BpmnParticipant[] participantField;

    private BpmnMessageFlow[] messageFlowField;

    private BpmnArtifact[] itemsField;

    private BpmnConversationNode[] items1Field;

    private BpmnConversationAssociation[] conversationAssociationField;

    private BpmnParticipantAssociation[] participantAssociationField;

    private BpmnMessageFlowAssociation[] messageFlowAssociationField;

    private BpmnCorrelationKey[] correlationKeyField;

    private XmlQualifiedName[] choreographyRefField;

    private BpmnConversationLink[] conversationLinkField;

    private string nameField;

    private bool isClosedField;

    public BpmnCollaboration()
    {
        isClosedField = false;
    }

    /// <remarks/>
    [XmlElement("participant")]
    public BpmnParticipant[] participant
    {
        get { return participantField; }
        set { participantField = value; }
    }

    /// <remarks/>
    [XmlElement("messageFlow")]
    public BpmnMessageFlow[] messageFlow
    {
        get { return messageFlowField; }
        set { messageFlowField = value; }
    }

    /// <remarks/>
    [XmlElement("artifact", typeof(BpmnArtifact))]
    [XmlElement("association", typeof(BpmnAssociation))]
    [XmlElement("group", typeof(BpmnGroup))]
    [XmlElement("textAnnotation", typeof(BpmnTextAnnotation))]
    public BpmnArtifact[] Items
    {
        get { return itemsField; }
        set { itemsField = value; }
    }

    /// <remarks/>
    [XmlElement("callConversation", typeof(BpmnCallConversation))]
    [XmlElement("conversation", typeof(BpmnConversation))]
    [XmlElement("conversationNode", typeof(BpmnConversationNode))]
    [XmlElement("subConversation", typeof(BpmnSubConversation))]
    public BpmnConversationNode[] Items1
    {
        get { return items1Field; }
        set { items1Field = value; }
    }

    /// <remarks/>
    [XmlElement("conversationAssociation")]
    public BpmnConversationAssociation[] conversationAssociation
    {
        get { return conversationAssociationField; }
        set { conversationAssociationField = value; }
    }

    /// <remarks/>
    [XmlElement("participantAssociation")]
    public BpmnParticipantAssociation[] participantAssociation
    {
        get { return participantAssociationField; }
        set { participantAssociationField = value; }
    }

    /// <remarks/>
    [XmlElement("messageFlowAssociation")]
    public BpmnMessageFlowAssociation[] messageFlowAssociation
    {
        get { return messageFlowAssociationField; }
        set { messageFlowAssociationField = value; }
    }

    /// <remarks/>
    [XmlElement("correlationKey")]
    public BpmnCorrelationKey[] correlationKey
    {
        get { return correlationKeyField; }
        set { correlationKeyField = value; }
    }

    /// <remarks/>
    [XmlElement("choreographyRef")]
    public XmlQualifiedName[] choreographyRef
    {
        get { return choreographyRefField; }
        set { choreographyRefField = value; }
    }

    /// <remarks/>
    [XmlElement("conversationLink")]
    public BpmnConversationLink[] conversationLink
    {
        get { return conversationLinkField; }
        set { conversationLinkField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string name
    {
        get { return nameField; }
        set { nameField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    [DefaultValue(false)]
    public bool isClosed
    {
        get { return isClosedField; }
        set { isClosedField = value; }
    }
}