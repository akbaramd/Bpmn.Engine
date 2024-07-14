namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("correlationSubscription", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable=false)]
public partial class BpmnCorrelationSubscription : BpmnBaseElement {
    
    private BpmnCorrelationPropertyBinding[] correlationPropertyBindingField;
    
    private System.Xml.XmlQualifiedName correlationKeyRefField;
    
    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("correlationPropertyBinding")]
    public BpmnCorrelationPropertyBinding[] correlationPropertyBinding {
        get {
            return correlationPropertyBindingField;
        }
        set {
            correlationPropertyBindingField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public System.Xml.XmlQualifiedName correlationKeyRef {
        get {
            return correlationKeyRefField;
        }
        set {
            correlationKeyRefField = value;
        }
    }
}