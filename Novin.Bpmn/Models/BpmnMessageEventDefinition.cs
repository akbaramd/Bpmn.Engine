namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("messageEventDefinition", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable=false)]
public partial class BpmnMessageEventDefinition : BpmnEventDefinition {
    
    private System.Xml.XmlQualifiedName operationRefField;
    
    private System.Xml.XmlQualifiedName messageRefField;
    
    /// <remarks/>
    public System.Xml.XmlQualifiedName operationRef {
        get {
            return operationRefField;
        }
        set {
            operationRefField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public System.Xml.XmlQualifiedName messageRef {
        get {
            return messageRefField;
        }
        set {
            messageRefField = value;
        }
    }
}