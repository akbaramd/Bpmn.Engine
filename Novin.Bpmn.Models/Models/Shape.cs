using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Serialization;

namespace Novin.Bpmn.Models;

/// <remarks/>
[XmlInclude(typeof(LabeledShape))]
[XmlInclude(typeof(BPMNShape))]
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.omg.org/spec/DD/20100524/DI")]
[XmlRoot(Namespace = "http://www.omg.org/spec/DD/20100524/DI", IsNullable = false)]
public abstract class Shape : Node
{
    private Bounds boundsField;

    /// <remarks/>
    [XmlElement(Namespace = "http://www.omg.org/spec/DD/20100524/DC")]
    public Bounds Bounds
    {
        get { return boundsField; }
        set { boundsField = value; }
    }
}