namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("conversationAssociation", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable=false)]
public partial class BpmnConversationAssociation : BpmnBaseElement {
    
    private System.Xml.XmlQualifiedName innerConversationNodeRefField;
    
    private System.Xml.XmlQualifiedName outerConversationNodeRefField;
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public System.Xml.XmlQualifiedName innerConversationNodeRef {
        get {
            return innerConversationNodeRefField;
        }
        set {
            innerConversationNodeRefField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public System.Xml.XmlQualifiedName outerConversationNodeRef {
        get {
            return outerConversationNodeRefField;
        }
        set {
            outerConversationNodeRefField = value;
        }
    }
}