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
[XmlRoot("errorEventDefinition",
    Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public class BpmnErrorEventDefinition : BpmnEventDefinition
{
    private XmlQualifiedName errorRefField;

    /// <remarks/>
    [XmlAttribute]
    public XmlQualifiedName errorRef
    {
        get { return errorRefField; }
        set { errorRefField = value; }
    }
}