#nullable disable
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
[XmlRoot("dataInput", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public class BpmnDataInput : BpmnBaseElement
{
    private BpmnDataState dataStateField;

    private string nameField;

    private XmlQualifiedName itemSubjectRefField;

    private bool isCollectionField;

    public BpmnDataInput()
    {
        isCollectionField = false;
    }

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

    /// <remarks/>
    [XmlAttribute]
    [DefaultValue(false)]
    public bool isCollection
    {
        get { return isCollectionField; }
        set { isCollectionField = value; }
    }
}