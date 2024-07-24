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
[XmlRoot("sendTask", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public class BpmnSendTask : BpmnTask
{
    private string implementationField;

    private XmlQualifiedName messageRefField;

    private XmlQualifiedName operationRefField;

    public BpmnSendTask()
    {
        implementationField = "##WebService";
    }

    /// <remarks/>
    [XmlAttribute]
    [DefaultValue("##WebService")]
    public string implementation
    {
        get { return implementationField; }
        set { implementationField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public XmlQualifiedName messageRef
    {
        get { return messageRefField; }
        set { messageRefField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public XmlQualifiedName operationRef
    {
        get { return operationRefField; }
        set { operationRefField = value; }
    }
}