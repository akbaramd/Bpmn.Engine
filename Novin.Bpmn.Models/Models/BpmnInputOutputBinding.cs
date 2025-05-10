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
[XmlRoot("ioBinding", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public class BpmnInputOutputBinding : BpmnBaseElement
{
    private XmlQualifiedName operationRefField;

    private string inputDataRefField;

    private string outputDataRefField;

    /// <remarks/>
    [XmlAttribute]
    public XmlQualifiedName operationRef
    {
        get { return operationRefField; }
        set { operationRefField = value; }
    }

    /// <remarks/>
    [XmlAttribute(DataType = "IDREF")]
    public string inputDataRef
    {
        get { return inputDataRefField; }
        set { inputDataRefField = value; }
    }

    /// <remarks/>
    [XmlAttribute(DataType = "IDREF")]
    public string outputDataRef
    {
        get { return outputDataRefField; }
        set { outputDataRefField = value; }
    }
}