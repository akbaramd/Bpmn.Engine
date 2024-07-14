namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("partnerRole", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable=false)]
public partial class BpmnPartnerRole : BpmnRootElement {
    
    private System.Xml.XmlQualifiedName[] participantRefField;
    
    private string nameField;
    
    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("participantRef")]
    public System.Xml.XmlQualifiedName[] participantRef {
        get {
            return participantRefField;
        }
        set {
            participantRefField = value;
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
}