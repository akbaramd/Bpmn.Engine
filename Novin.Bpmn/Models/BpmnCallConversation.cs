namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("callConversation", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable=false)]
public partial class BpmnCallConversation : BpmnConversationNode {
    
    private BpmnParticipantAssociation[] participantAssociationField;
    
    private System.Xml.XmlQualifiedName calledCollaborationRefField;
    
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
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public System.Xml.XmlQualifiedName calledCollaborationRef {
        get {
            return calledCollaborationRefField;
        }
        set {
            calledCollaborationRefField = value;
        }
    }
}