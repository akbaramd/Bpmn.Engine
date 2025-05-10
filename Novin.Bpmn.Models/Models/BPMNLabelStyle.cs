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
[XmlType(Namespace = "http://www.omg.org/spec/BPMN/20100524/DI")]
[XmlRoot(Namespace = "http://www.omg.org/spec/BPMN/20100524/DI", IsNullable = false)]
public class BPMNLabelStyle : Style
{
    private Font fontField;

    /// <remarks/>
    [XmlElement(Namespace = "http://www.omg.org/spec/DD/20100524/DC")]
    public Font Font
    {
        get { return fontField; }
        set { fontField = value; }
    }
}