using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml;
using System.Xml.Serialization;

namespace Novin.Bpmn.Models;

/// <remarks />
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlRoot("boundaryEvent", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public class BpmnBoundaryEvent : BpmnCatchEvent
{
    private XmlQualifiedName attachedToRefField;

    private bool cancelActivityField;

    public BpmnBoundaryEvent()
    {
        cancelActivityField = true;
    }

    /// <remarks />
    [XmlAttribute]
    [DefaultValue(true)]
    public bool cancelActivity
    {
        get => cancelActivityField;
        set => cancelActivityField = value;
    }

    /// <remarks />
    [XmlAttribute]
    public XmlQualifiedName attachedToRef
    {
        get => attachedToRefField;
        set => attachedToRefField = value;
    }
}