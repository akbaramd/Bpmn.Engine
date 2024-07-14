using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml;
using System.Xml.Serialization;

namespace Novin.Bpmn.Test.Models;

/// <remarks />
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlRoot("callActivity", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public class BpmnCallActivity : BpmnActivity
{
    private XmlQualifiedName calledElementField;

    /// <remarks />
    [XmlAttribute]
    public XmlQualifiedName calledElement
    {
        get => calledElementField;
        set => calledElementField = value;
    }
}