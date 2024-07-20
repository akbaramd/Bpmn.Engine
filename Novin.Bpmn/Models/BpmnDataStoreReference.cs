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
[XmlRoot("dataStoreReference",
    Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public class BpmnDataStoreReference : BpmnFlowElement
{
    private BpmnDataState dataStateField;

    private XmlQualifiedName dataStoreRefField;

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
    [XmlAttribute]
    public XmlQualifiedName dataStoreRef
    {
        get => dataStoreRefField;
        set => dataStoreRefField = value;
    }
}