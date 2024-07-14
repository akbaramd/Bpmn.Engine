using System.Xml.Serialization;

namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[XmlType(Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", TypeName = "tFormalExpression")]
[XmlRoot(ElementName = "formalExpression", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable=false)]
public partial class BpmnFormalExpression : BpmnExpression {
    
    private string languageField;
    
    private System.Xml.XmlQualifiedName evaluatesToTypeRefField;
    
    /// <remarks/>
    [XmlAttribute(DataType="anyURI")]
    public string language {
        get {
            return languageField;
        }
        set {
            languageField = value;
        }
    }


    /// <remarks/>
    [XmlAttribute()]
    public System.Xml.XmlQualifiedName evaluatesToTypeRef {
        get {
            return evaluatesToTypeRefField;
        }
        set {
            evaluatesToTypeRefField = value;
        }
    }
    
    [XmlAttribute("type", Namespace = "http://www.w3.org/2001/XMLSchema-instance")]
    public string Type { get; set; }

}