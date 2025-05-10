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
[XmlRoot("lane", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public class BpmnLane : BpmnBaseElement
{
    private BpmnLaneSet childLaneSetField;

    private string[] flowNodeRefField;

    private string nameField;

    private BpmnBaseElement partitionElementField;

    private XmlQualifiedName partitionElementRefField;

    /// <remarks />
    public BpmnBaseElement partitionElement
    {
        get => partitionElementField;
        set => partitionElementField = value;
    }

    /// <remarks />
    [XmlElement("flowNodeRef", DataType = "IDREF")]
    public string[] flowNodeRef
    {
        get => flowNodeRefField;
        set => flowNodeRefField = value;
    }

    /// <remarks />
    public BpmnLaneSet childLaneSet
    {
        get => childLaneSetField;
        set => childLaneSetField = value;
    }

    /// <remarks />
    [XmlAttribute]
    public string name
    {
        get => nameField;
        set => nameField = value;
    }

    /// <remarks />
    [XmlAttribute]
    public XmlQualifiedName partitionElementRef
    {
        get => partitionElementRefField;
        set => partitionElementRefField = value;
    }
}