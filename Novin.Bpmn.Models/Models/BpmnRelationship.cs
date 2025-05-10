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
[XmlRoot("relationship", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public class BpmnRelationship : BpmnBaseElement
{
    private XmlQualifiedName[] sourceField;

    private XmlQualifiedName[] targetField;

    private string typeField;

    private BpmnRelationshipDirection directionField;

    private bool directionFieldSpecified;

    /// <remarks/>
    [XmlElement("source")]
    public XmlQualifiedName[] source
    {
        get { return sourceField; }
        set { sourceField = value; }
    }

    /// <remarks/>
    [XmlElement("target")]
    public XmlQualifiedName[] target
    {
        get { return targetField; }
        set { targetField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string type
    {
        get { return typeField; }
        set { typeField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public BpmnRelationshipDirection direction
    {
        get { return directionField; }
        set { directionField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool directionSpecified
    {
        get { return directionFieldSpecified; }
        set { directionFieldSpecified = value; }
    }
}