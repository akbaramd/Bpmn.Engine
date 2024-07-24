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
[XmlRoot("serviceTask", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public class BpmnServiceTask : BpmnTask
{
    private string implementationField;

    private XmlQualifiedName operationRefField;

    public BpmnServiceTask()
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
    public XmlQualifiedName operationRef
    {
        get { return operationRefField; }
        set { operationRefField = value; }
    }
}