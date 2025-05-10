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
[XmlRoot("messageFlowAssociation",
    Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public class BpmnMessageFlowAssociation : BpmnBaseElement
{
    private XmlQualifiedName innerMessageFlowRefField;

    private XmlQualifiedName outerMessageFlowRefField;

    /// <remarks/>
    [XmlAttribute]
    public XmlQualifiedName innerMessageFlowRef
    {
        get { return innerMessageFlowRefField; }
        set { innerMessageFlowRefField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public XmlQualifiedName outerMessageFlowRef
    {
        get { return outerMessageFlowRefField; }
        set { outerMessageFlowRefField = value; }
    }
}