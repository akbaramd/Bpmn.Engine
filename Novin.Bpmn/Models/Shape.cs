namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.Xml.Serialization.XmlIncludeAttribute(typeof(LabeledShape))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BPMNShape))]
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.omg.org/spec/DD/20100524/DI")]
[System.Xml.Serialization.XmlRootAttribute(Namespace = "http://www.omg.org/spec/DD/20100524/DI", IsNullable = false)]
public abstract partial class Shape : Node
{
    private Bounds boundsField;

    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute(Namespace = "http://www.omg.org/spec/DD/20100524/DC")]
    public Bounds Bounds
    {
        get { return boundsField; }
        set { boundsField = value; }
    }
}