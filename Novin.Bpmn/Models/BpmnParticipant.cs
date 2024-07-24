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
[XmlRoot("participant", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public class BpmnParticipant : BpmnBaseElement
{
    private XmlQualifiedName[] interfaceRefField;

    private XmlQualifiedName[] endPointRefField;

    private BpmnParticipantMultiplicity participantMultiplicityField;

    private string nameField;

    private XmlQualifiedName processRefField;

    /// <remarks/>
    [XmlElement("interfaceRef")]
    public XmlQualifiedName[] interfaceRef
    {
        get { return interfaceRefField; }
        set { interfaceRefField = value; }
    }

    /// <remarks/>
    [XmlElement("endPointRef")]
    public XmlQualifiedName[] endPointRef
    {
        get { return endPointRefField; }
        set { endPointRefField = value; }
    }

    /// <remarks/>
    public BpmnParticipantMultiplicity participantMultiplicity
    {
        get { return participantMultiplicityField; }
        set { participantMultiplicityField = value; }
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
    public XmlQualifiedName processRef
    {
        get { return processRefField; }
        set { processRefField = value; }
    }
}