using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Serialization;

namespace Novin.Bpmn.Test.Models;

/// <remarks />
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlRoot("laneSet", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public class BpmnLaneSet : BpmnBaseElement
{
    private BpmnLane[] laneField;

    private string nameField;

    /// <remarks />
    [XmlElement("lane")]
    public BpmnLane[] lane
    {
        get => laneField;
        set => laneField = value;
    }

    /// <remarks />
    [XmlAttribute]
    public string name
    {
        get => nameField;
        set => nameField = value;
    }
}