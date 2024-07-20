namespace Novin.Bpmn.Models;

/// <remarks/>
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnSubConversation))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnConversation))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnCallConversation))]
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("conversationNode",
    Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public abstract partial class BpmnConversationNode : BpmnBaseElement
{
    private System.Xml.XmlQualifiedName[] participantRefField;

    private System.Xml.XmlQualifiedName[] messageFlowRefField;

    private BpmnCorrelationKey[] correlationKeyField;

    private string nameField;

    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("participantRef")]
    public System.Xml.XmlQualifiedName[] participantRef
    {
        get { return participantRefField; }
        set { participantRefField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("messageFlowRef")]
    public System.Xml.XmlQualifiedName[] messageFlowRef
    {
        get { return messageFlowRefField; }
        set { messageFlowRefField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("correlationKey")]
    public BpmnCorrelationKey[] correlationKey
    {
        get { return correlationKeyField; }
        set { correlationKeyField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public string name
    {
        get { return nameField; }
        set { nameField = value; }
    }
}