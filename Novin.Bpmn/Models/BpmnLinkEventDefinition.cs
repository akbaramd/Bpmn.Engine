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
[XmlRoot("linkEventDefinition",
    Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public class BpmnLinkEventDefinition : BpmnEventDefinition
{
    private XmlQualifiedName[] sourceField;

    private XmlQualifiedName targetField;

    private string nameField;

    /// <remarks/>
    [XmlElement("source")]
    public XmlQualifiedName[] source
    {
        get { return sourceField; }
        set { sourceField = value; }
    }

    /// <remarks/>
    public XmlQualifiedName target
    {
        get { return targetField; }
        set { targetField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string name
    {
        get { return nameField; }
        set { nameField = value; }
    }
}