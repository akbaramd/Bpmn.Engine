namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("resourceParameterBinding", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable=false)]
public partial class BpmnResourceParameterBinding : BpmnBaseElement {
    
    private BpmnExpression itemField;
    
    private System.Xml.XmlQualifiedName parameterRefField;
    
    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("expression", typeof(BpmnExpression))]
    [System.Xml.Serialization.XmlElementAttribute("formalExpression", typeof(BpmnFormalExpression))]
    public BpmnExpression Item {
        get {
            return itemField;
        }
        set {
            itemField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public System.Xml.XmlQualifiedName parameterRef {
        get {
            return parameterRefField;
        }
        set {
            parameterRefField = value;
        }
    }
}