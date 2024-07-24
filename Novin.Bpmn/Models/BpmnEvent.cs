using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Serialization;

namespace Novin.Bpmn.Models;

/// <remarks/>
[XmlInclude(typeof(BpmnThrowEvent))]
[XmlInclude(typeof(BpmnIntermediateThrowEvent))]
[XmlInclude(typeof(BpmnImplicitThrowEvent))]
[XmlInclude(typeof(BpmnEndEvent))]
[XmlInclude(typeof(BpmnCatchEvent))]
[XmlInclude(typeof(BpmnStartEvent))]
[XmlInclude(typeof(BpmnIntermediateCatchEvent))]
[XmlInclude(typeof(BpmnBoundaryEvent))]
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlRoot("event", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public abstract class BpmnEvent : BpmnFlowNode
{
    private BpmnProperty[] propertyField;

    /// <remarks/>
    [XmlElement("property")]
    public BpmnProperty[] property
    {
        get { return propertyField; }
        set { propertyField = value; }
    }
}