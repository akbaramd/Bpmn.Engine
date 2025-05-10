using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml;
using System.Xml.Serialization;

namespace Novin.Bpmn.Models;

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.omg.org/spec/BPMN/20100524/DI")]
[XmlRoot(Namespace = "http://www.omg.org/spec/BPMN/20100524/DI", IsNullable = false)]
public class BPMNPlane : Plane
{
    private XmlQualifiedName bpmnElementField;

    /// <remarks/>
    [XmlAttribute]
    public XmlQualifiedName bpmnElement
    {
        get { return bpmnElementField; }
        set { bpmnElementField = value; }
    }
}