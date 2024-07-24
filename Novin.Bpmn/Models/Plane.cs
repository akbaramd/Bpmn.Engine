using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Serialization;

namespace Novin.Bpmn.Models;

/// <remarks/>
[XmlInclude(typeof(BPMNPlane))]
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.omg.org/spec/DD/20100524/DI")]
[XmlRoot(Namespace = "http://www.omg.org/spec/DD/20100524/DI", IsNullable = false)]
public abstract class Plane : Node
{
    private DiagramElement[] diagramElement1Field;

    /// <remarks/>
    [XmlElement("DiagramElement")]
    public DiagramElement[] DiagramElement1
    {
        get { return diagramElement1Field; }
        set { diagramElement1Field = value; }
    }
}