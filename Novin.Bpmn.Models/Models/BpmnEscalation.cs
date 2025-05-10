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
[XmlRoot("escalation", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public class BpmnEscalation : BpmnRootElement
{
    private string nameField;

    private string escalationCodeField;

    private XmlQualifiedName structureRefField;

    /// <remarks/>
    [XmlAttribute]
    public string name
    {
        get { return nameField; }
        set { nameField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string escalationCode
    {
        get { return escalationCodeField; }
        set { escalationCodeField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public XmlQualifiedName structureRef
    {
        get { return structureRefField; }
        set { structureRefField = value; }
    }
}