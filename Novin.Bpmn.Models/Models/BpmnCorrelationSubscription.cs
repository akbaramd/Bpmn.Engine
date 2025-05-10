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
[XmlType(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlRoot("correlationSubscription",
    Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public class BpmnCorrelationSubscription : BpmnBaseElement
{
    private BpmnCorrelationPropertyBinding[] correlationPropertyBindingField;

    private XmlQualifiedName correlationKeyRefField;

    /// <remarks/>
    [XmlElement("correlationPropertyBinding")]
    public BpmnCorrelationPropertyBinding[] correlationPropertyBinding
    {
        get { return correlationPropertyBindingField; }
        set { correlationPropertyBindingField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public XmlQualifiedName correlationKeyRef
    {
        get { return correlationKeyRefField; }
        set { correlationKeyRefField = value; }
    }
}