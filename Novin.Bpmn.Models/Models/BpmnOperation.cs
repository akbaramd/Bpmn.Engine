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
[XmlRoot("operation", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public class BpmnOperation : BpmnBaseElement
{
    private XmlQualifiedName inMessageRefField;

    private XmlQualifiedName outMessageRefField;

    private XmlQualifiedName[] errorRefField;

    private string nameField;

    private XmlQualifiedName implementationRefField;

    /// <remarks/>
    public XmlQualifiedName inMessageRef
    {
        get { return inMessageRefField; }
        set { inMessageRefField = value; }
    }

    /// <remarks/>
    public XmlQualifiedName outMessageRef
    {
        get { return outMessageRefField; }
        set { outMessageRefField = value; }
    }

    /// <remarks/>
    [XmlElement("errorRef")]
    public XmlQualifiedName[] errorRef
    {
        get { return errorRefField; }
        set { errorRefField = value; }
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
    public XmlQualifiedName implementationRef
    {
        get { return implementationRefField; }
        set { implementationRefField = value; }
    }
}