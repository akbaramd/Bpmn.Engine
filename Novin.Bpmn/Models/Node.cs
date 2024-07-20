namespace Novin.Bpmn.Models;

/// <remarks/>
[System.Xml.Serialization.XmlIncludeAttribute(typeof(Plane))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BPMNPlane))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(Label))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BPMNLabel))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(Shape))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(LabeledShape))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BPMNShape))]
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.omg.org/spec/DD/20100524/DI")]
[System.Xml.Serialization.XmlRootAttribute(Namespace = "http://www.omg.org/spec/DD/20100524/DI", IsNullable = false)]
public abstract partial class Node : DiagramElement
{
}