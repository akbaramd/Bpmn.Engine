using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Serialization;

namespace Novin.Bpmn.Models;

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlRoot("participantMultiplicity",
    Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public class BpmnParticipantMultiplicity : BpmnBaseElement
{
    private int minimumField;

    private int maximumField;

    public BpmnParticipantMultiplicity()
    {
        minimumField = 0;
        maximumField = 1;
    }

    /// <remarks/>
    [XmlAttribute]
    [DefaultValue(0)]
    public int minimum
    {
        get { return minimumField; }
        set { minimumField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    [DefaultValue(1)]
    public int maximum
    {
        get { return maximumField; }
        set { maximumField = value; }
    }
}