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
[XmlRoot("property", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public class BpmnProperty : BpmnBaseElement
{
    private BpmnDataState dataStateField;

    private string nameField;

    private XmlQualifiedName itemSubjectRefField;

    /// <remarks/>
    public BpmnDataState dataState
    {
        get { return dataStateField; }
        set { dataStateField = value; }
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
    public XmlQualifiedName itemSubjectRef
    {
        get { return itemSubjectRefField; }
        set { itemSubjectRefField = value; }
    }
}