namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnGlobalConversation))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnChoreography))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnGlobalChoreographyTask))]
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("collaboration", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable=false)]
public partial class BpmnCollaboration : BpmnRootElement {
    
    private BpmnParticipant[] participantField;
    
    private BpmnMessageFlow[] messageFlowField;
    
    private BpmnArtifact[] itemsField;
    
    private BpmnConversationNode[] items1Field;
    
    private BpmnConversationAssociation[] conversationAssociationField;
    
    private BpmnParticipantAssociation[] participantAssociationField;
    
    private BpmnMessageFlowAssociation[] messageFlowAssociationField;
    
    private BpmnCorrelationKey[] correlationKeyField;
    
    private System.Xml.XmlQualifiedName[] choreographyRefField;
    
    private BpmnConversationLink[] conversationLinkField;
    
    private string nameField;
    
    private bool isClosedField;
    
    public BpmnCollaboration() {
        isClosedField = false;
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("participant")]
    public BpmnParticipant[] participant {
        get {
            return participantField;
        }
        set {
            participantField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("messageFlow")]
    public BpmnMessageFlow[] messageFlow {
        get {
            return messageFlowField;
        }
        set {
            messageFlowField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("artifact", typeof(BpmnArtifact))]
    [System.Xml.Serialization.XmlElementAttribute("association", typeof(BpmnAssociation))]
    [System.Xml.Serialization.XmlElementAttribute("group", typeof(BpmnGroup))]
    [System.Xml.Serialization.XmlElementAttribute("textAnnotation", typeof(BpmnTextAnnotation))]
    public BpmnArtifact[] Items {
        get {
            return itemsField;
        }
        set {
            itemsField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("callConversation", typeof(BpmnCallConversation))]
    [System.Xml.Serialization.XmlElementAttribute("conversation", typeof(BpmnConversation))]
    [System.Xml.Serialization.XmlElementAttribute("conversationNode", typeof(BpmnConversationNode))]
    [System.Xml.Serialization.XmlElementAttribute("subConversation", typeof(BpmnSubConversation))]
    public BpmnConversationNode[] Items1 {
        get {
            return items1Field;
        }
        set {
            items1Field = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("conversationAssociation")]
    public BpmnConversationAssociation[] conversationAssociation {
        get {
            return conversationAssociationField;
        }
        set {
            conversationAssociationField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("participantAssociation")]
    public BpmnParticipantAssociation[] participantAssociation {
        get {
            return participantAssociationField;
        }
        set {
            participantAssociationField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("messageFlowAssociation")]
    public BpmnMessageFlowAssociation[] messageFlowAssociation {
        get {
            return messageFlowAssociationField;
        }
        set {
            messageFlowAssociationField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("correlationKey")]
    public BpmnCorrelationKey[] correlationKey {
        get {
            return correlationKeyField;
        }
        set {
            correlationKeyField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("choreographyRef")]
    public System.Xml.XmlQualifiedName[] choreographyRef {
        get {
            return choreographyRefField;
        }
        set {
            choreographyRefField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("conversationLink")]
    public BpmnConversationLink[] conversationLink {
        get {
            return conversationLinkField;
        }
        set {
            conversationLinkField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public string name {
        get {
            return nameField;
        }
        set {
            nameField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    [System.ComponentModel.DefaultValueAttribute(false)]
    public bool isClosed {
        get {
            return isClosedField;
        }
        set {
            isClosedField = value;
        }
    }
}