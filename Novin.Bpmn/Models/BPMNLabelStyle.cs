namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace="http://www.omg.org/spec/BPMN/20100524/DI")]
[System.Xml.Serialization.XmlRootAttribute(Namespace="http://www.omg.org/spec/BPMN/20100524/DI", IsNullable=false)]
public partial class BPMNLabelStyle : Style {
    
    private Font fontField;
    
    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute(Namespace="http://www.omg.org/spec/DD/20100524/DC")]
    public Font Font {
        get {
            return fontField;
        }
        set {
            fontField = value;
        }
    }
}