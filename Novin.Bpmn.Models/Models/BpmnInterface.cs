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
[XmlRoot("interface", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public class BpmnInterface : BpmnRootElement
{
    private BpmnOperation[] operationField;

    private string nameField;

    private XmlQualifiedName implementationRefField;

    /// <remarks/>
    [XmlElement("operation")]
    public BpmnOperation[] operation
    {
        get { return operationField; }
        set { operationField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string name
    {
        get { return nameField; }
        set { nameField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public XmlQualifiedName implementationRef
    {
        get { return implementationRefField; }
        set { implementationRefField = value; }
    }
}