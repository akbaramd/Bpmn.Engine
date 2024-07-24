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
[XmlRoot("dataStore", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public class BpmnDataStore : BpmnRootElement
{
    private BpmnDataState dataStateField;

    private string nameField;

    private string capacityField;

    private bool isUnlimitedField;

    private XmlQualifiedName itemSubjectRefField;

    public BpmnDataStore()
    {
        isUnlimitedField = true;
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
    [XmlAttribute(DataType = "integer")]
    public string capacity
    {
        get { return capacityField; }
        set { capacityField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    [DefaultValue(true)]
    public bool isUnlimited
    {
        get { return isUnlimitedField; }
        set { isUnlimitedField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public XmlQualifiedName itemSubjectRef
    {
        get { return itemSubjectRefField; }
        set { itemSubjectRefField = value; }
    }
}