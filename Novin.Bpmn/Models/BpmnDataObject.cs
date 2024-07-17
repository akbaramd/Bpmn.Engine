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
[XmlRoot("dataObject", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public class BpmnDataObject : BpmnFlowElement
{
    private BpmnDataState dataStateField;

    private bool isCollectionField;

    private XmlQualifiedName itemSubjectRefField;

    public BpmnDataObject()
    {
        isCollectionField = false;
    }

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
    [DefaultValue(false)]
    public bool isCollection
    {
        get => isCollectionField;
        set => isCollectionField = value;
    }
}