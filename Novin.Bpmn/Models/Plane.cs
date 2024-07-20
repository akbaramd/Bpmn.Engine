namespace Novin.Bpmn.Models;

/// <remarks/>
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BPMNPlane))]
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.omg.org/spec/DD/20100524/DI")]
[System.Xml.Serialization.XmlRootAttribute(Namespace = "http://www.omg.org/spec/DD/20100524/DI", IsNullable = false)]
public abstract partial class Plane : Node
{
    private DiagramElement[] diagramElement1Field;

    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("DiagramElement")]
    public DiagramElement[] DiagramElement1
    {
        get { return diagramElement1Field; }
        set { diagramElement1Field = value; }
    }
}