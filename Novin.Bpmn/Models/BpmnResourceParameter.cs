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
[XmlRoot("resourceParameter",
    Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public class BpmnResourceParameter : BpmnBaseElement
{
    private string nameField;

    private XmlQualifiedName typeField;

    private bool isRequiredField;

    private bool isRequiredFieldSpecified;

    /// <remarks/>
    [XmlAttribute]
    public string name
    {
        get { return nameField; }
        set { nameField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public XmlQualifiedName type
    {
        get { return typeField; }
        set { typeField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public bool isRequired
    {
        get { return isRequiredField; }
        set { isRequiredField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool isRequiredSpecified
    {
        get { return isRequiredFieldSpecified; }
        set { isRequiredFieldSpecified = value; }
    }
}