namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(AnonymousType=true, Namespace="http://www.omg.org/spec/DD/20100524/DI")]
public partial class DiagramElementExtension {
    
    private System.Xml.XmlElement[] anyField;
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAnyElementAttribute()]
    public System.Xml.XmlElement[] Any {
        get {
            return anyField;
        }
        set {
            anyField = value;
        }
    }
}