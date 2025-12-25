using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml;
using System.Xml.Serialization;

namespace Novin.Bpmn.Models.Models;

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlRoot("extensionElements",
    Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public class BpmnExtensionElements
{
    private XmlElement[] anyField;

    /// <remarks/>
    [XmlAnyElement]
    public XmlElement[] Any
    {
        get { return anyField; }
        set { anyField = value; }
    }

    /// <summary>
    /// Bonyan IO Mapping extension - for input/output variable mapping
    /// This should be inside extensionElements according to BPMN 2.0 specification
    /// </summary>
    [XmlElement("ioMapping", Namespace = BpmnXmlNamespaces.Bonyan)]
    public BonyanIoMapping? BonyanIoMapping { get; set; }

    /// <summary>
    /// Bonyan IO extension - alternative IO mapping format
    /// </summary>
    [XmlElement("io", Namespace = BpmnXmlNamespaces.Bonyan)]
    public BonyanIo? BonyanIo { get; set; }
}