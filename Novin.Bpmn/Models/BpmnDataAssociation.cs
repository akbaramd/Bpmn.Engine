namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnDataOutputAssociation))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnDataInputAssociation))]
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("dataAssociation", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable=false)]
public partial class BpmnDataAssociation : BpmnBaseElement {
    
    private string[] sourceRefField;
    
    private string targetRefField;
    
    private BpmnFormalExpression transformationField;
    
    private BpmnAssignment[] assignmentField;
    
    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("sourceRef", DataType="IDREF")]
    public string[] sourceRef {
        get {
            return sourceRefField;
        }
        set {
            sourceRefField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute(DataType="IDREF")]
    public string targetRef {
        get {
            return targetRefField;
        }
        set {
            targetRefField = value;
        }
    }
    
    /// <remarks/>
    public BpmnFormalExpression transformation {
        get {
            return transformationField;
        }
        set {
            transformationField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("assignment")]
    public BpmnAssignment[] assignment {
        get {
            return assignmentField;
        }
        set {
            assignmentField = value;
        }
    }
}