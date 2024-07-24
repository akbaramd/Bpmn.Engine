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
[XmlRoot("import", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public class BpmnImport
{
    private string namespaceField;

    private string locationField;

    private string importTypeField;

    /// <remarks/>
    [XmlAttribute(DataType = "anyURI")]
    public string @namespace
    {
        get { return namespaceField; }
        set { namespaceField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string location
    {
        get { return locationField; }
        set { locationField = value; }
    }

    /// <remarks/>
    [XmlAttribute(DataType = "anyURI")]
    public string importType
    {
        get { return importTypeField; }
        set { importTypeField = value; }
    }
}