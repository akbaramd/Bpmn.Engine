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
[XmlRoot("extension", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public class BpmnExtension
{
    private BpmnDocumentation[] documentationField;

    private XmlQualifiedName definitionField;

    private bool mustUnderstandField;

    public BpmnExtension()
    {
        mustUnderstandField = false;
    }

    /// <remarks/>
    [XmlElement("documentation")]
    public BpmnDocumentation[] documentation
    {
        get { return documentationField; }
        set { documentationField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public XmlQualifiedName definition
    {
        get { return definitionField; }
        set { definitionField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    [DefaultValue(false)]
    public bool mustUnderstand
    {
        get { return mustUnderstandField; }
        set { mustUnderstandField = value; }
    }
}