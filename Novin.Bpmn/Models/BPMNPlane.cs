namespace Novin.Bpmn.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.omg.org/spec/BPMN/20100524/DI")]
[System.Xml.Serialization.XmlRootAttribute(Namespace = "http://www.omg.org/spec/BPMN/20100524/DI", IsNullable = false)]
public partial class BPMNPlane : Plane
{
    private System.Xml.XmlQualifiedName bpmnElementField;

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public System.Xml.XmlQualifiedName bpmnElement
    {
        get { return bpmnElementField; }
        set { bpmnElementField = value; }
    }
}