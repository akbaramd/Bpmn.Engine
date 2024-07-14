namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("relationship", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable=false)]
public partial class BpmnRelationship : BpmnBaseElement {
    
    private System.Xml.XmlQualifiedName[] sourceField;
    
    private System.Xml.XmlQualifiedName[] targetField;
    
    private string typeField;
    
    private BpmnRelationshipDirection directionField;
    
    private bool directionFieldSpecified;
    
    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("source")]
    public System.Xml.XmlQualifiedName[] source {
        get {
            return sourceField;
        }
        set {
            sourceField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("target")]
    public System.Xml.XmlQualifiedName[] target {
        get {
            return targetField;
        }
        set {
            targetField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public string type {
        get {
            return typeField;
        }
        set {
            typeField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public BpmnRelationshipDirection direction {
        get {
            return directionField;
        }
        set {
            directionField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlIgnoreAttribute()]
    public bool directionSpecified {
        get {
            return directionFieldSpecified;
        }
        set {
            directionFieldSpecified = value;
        }
    }
}