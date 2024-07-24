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
[XmlType(AnonymousType = true, Namespace = "http://www.omg.org/spec/DD/20100524/DI")]
public class DiagramElementExtension
{
    private XmlElement[] anyField;

    /// <remarks/>
    [XmlAnyElement]
    public XmlElement[] Any
    {
        get { return anyField; }
        set { anyField = value; }
    }
}