namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.Xml.Serialization.XmlIncludeAttribute(typeof(Edge))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(LabeledEdge))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BPMNEdge))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(Node))]
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
[System.Xml.Serialization.XmlTypeAttribute(Namespace="http://www.omg.org/spec/DD/20100524/DI")]
[System.Xml.Serialization.XmlRootAttribute(Namespace="http://www.omg.org/spec/DD/20100524/DI", IsNullable=false)]
public abstract partial class DiagramElement {
    
    private DiagramElementExtension extensionField;
    
    private string idField;
    
    private System.Xml.XmlAttribute[] anyAttrField;
    
    /// <remarks/>
    public DiagramElementExtension extension {
        get {
            return extensionField;
        }
        set {
            extensionField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute(DataType="ID")]
    public string id {
        get {
            return idField;
        }
        set {
            idField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAnyAttributeAttribute()]
    public System.Xml.XmlAttribute[] AnyAttr {
        get {
            return anyAttrField;
        }
        set {
            anyAttrField = value;
        }
    }
}