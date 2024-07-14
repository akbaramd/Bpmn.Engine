namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("sequenceFlow", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable=false)]
public partial class BpmnSequenceFlow : BpmnFlowElement {
    
    private BpmnExpression conditionExpressionField;
    
    private string sourceRefField;
    
    private string targetRefField;
    
    private bool isImmediateField;
    
    private bool isImmediateFieldSpecified;
    
    /// <remarks/>
    public BpmnExpression conditionExpression {
        get {
            return conditionExpressionField;
        }
        set {
            conditionExpressionField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute(DataType="IDREF")]
    public string sourceRef {
        get {
            return sourceRefField;
        }
        set {
            sourceRefField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute(DataType="IDREF")]
    public string targetRef {
        get {
            return targetRefField;
        }
        set {
            targetRefField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public bool isImmediate {
        get {
            return isImmediateField;
        }
        set {
            isImmediateField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlIgnoreAttribute()]
    public bool isImmediateSpecified {
        get {
            return isImmediateFieldSpecified;
        }
        set {
            isImmediateFieldSpecified = value;
        }
    }
}