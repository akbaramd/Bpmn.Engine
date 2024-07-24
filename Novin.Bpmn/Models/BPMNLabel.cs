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
[XmlType(Namespace = "http://www.omg.org/spec/BPMN/20100524/DI")]
[XmlRoot(Namespace = "http://www.omg.org/spec/BPMN/20100524/DI", IsNullable = false)]
public class BPMNLabel : Label
{
    private XmlQualifiedName labelStyleField;

    /// <remarks/>
    [XmlAttribute]
    public XmlQualifiedName labelStyle
    {
        get { return labelStyleField; }
        set { labelStyleField = value; }
    }
}