using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml;
using System.Xml.Serialization;

namespace Novin.Bpmn.Test.Models;

/// <remarks />
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlRoot("dataObjectReference",
    Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public class BpmnDataObjectReference : BpmnFlowElement
{
    private string dataObjectRefField;
    private BpmnDataState dataStateField;

    private XmlQualifiedName itemSubjectRefField;

    /// <remarks />
    public BpmnDataState dataState
    {
        get => dataStateField;
        set => dataStateField = value;
    }

    /// <remarks />
    [XmlAttribute]
    public XmlQualifiedName itemSubjectRef
    {
        get => itemSubjectRefField;
        set => itemSubjectRefField = value;
    }

    /// <remarks />
    [XmlAttribute(DataType = "IDREF")]
    public string dataObjectRef
    {
        get => dataObjectRefField;
        set => dataObjectRefField = value;
    }
}