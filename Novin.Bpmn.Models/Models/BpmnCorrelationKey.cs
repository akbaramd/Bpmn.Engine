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
[XmlRoot("correlationKey", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public class BpmnCorrelationKey : BpmnBaseElement
{
    private XmlQualifiedName[] correlationPropertyRefField;

    private string nameField;

    /// <remarks />
    [XmlElement("correlationPropertyRef")]
    public XmlQualifiedName[] correlationPropertyRef
    {
        get => correlationPropertyRefField;
        set => correlationPropertyRefField = value;
    }

    /// <remarks />
    [XmlAttribute]
    public string name
    {
        get => nameField;
        set => nameField = value;
    }
}