namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace="http://www.omg.org/spec/BPMN/20100524/DI")]
[System.Xml.Serialization.XmlRootAttribute(Namespace="http://www.omg.org/spec/BPMN/20100524/DI", IsNullable=false)]
public partial class BPMNDiagram : Diagram {
    
    private BPMNPlane bPMNPlaneField;
    
    private BPMNLabelStyle[] bPMNLabelStyleField;
    
    /// <remarks/>
    public BPMNPlane BPMNPlane {
        get {
            return bPMNPlaneField;
        }
        set {
            bPMNPlaneField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("BPMNLabelStyle")]
    public BPMNLabelStyle[] BPMNLabelStyle {
        get {
            return bPMNLabelStyleField;
        }
        set {
            bPMNLabelStyleField = value;
        }
    }
}