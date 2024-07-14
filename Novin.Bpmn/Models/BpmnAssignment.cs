namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("assignment", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable=false)]
public partial class BpmnAssignment : BpmnBaseElement {
    
    private BpmnExpression fromField;
    
    private BpmnExpression toField;
    
    /// <remarks/>
    public BpmnExpression from {
        get {
            return fromField;
        }
        set {
            fromField = value;
        }
    }
    
    /// <remarks/>
    public BpmnExpression to {
        get {
            return toField;
        }
        set {
            toField = value;
        }
    }
}