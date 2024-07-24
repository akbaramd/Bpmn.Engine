using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Serialization;

namespace Novin.Bpmn.Models;

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.omg.org/spec/BPMN/20100524/DI")]
[XmlRoot(Namespace = "http://www.omg.org/spec/BPMN/20100524/DI", IsNullable = false)]
public class BPMNDiagram : Diagram
{
    private BPMNPlane bPMNPlaneField;

    private BPMNLabelStyle[] bPMNLabelStyleField;

    /// <remarks/>
    public BPMNPlane BPMNPlane
    {
        get { return bPMNPlaneField; }
        set { bPMNPlaneField = value; }
    }

    /// <remarks/>
    [XmlElement("BPMNLabelStyle")]
    public BPMNLabelStyle[] BPMNLabelStyle
    {
        get { return bPMNLabelStyleField; }
        set { bPMNLabelStyleField = value; }
    }
}