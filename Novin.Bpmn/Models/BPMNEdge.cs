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
public class BPMNEdge : LabeledEdge
{
    private BPMNLabel bPMNLabelField;

    private XmlQualifiedName bpmnElementField;

    private XmlQualifiedName sourceElementField;

    private XmlQualifiedName targetElementField;

    private MessageVisibleKind messageVisibleKindField;

    private bool messageVisibleKindFieldSpecified;

    /// <remarks/>
    public BPMNLabel BPMNLabel
    {
        get { return bPMNLabelField; }
        set { bPMNLabelField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public XmlQualifiedName bpmnElement
    {
        get { return bpmnElementField; }
        set { bpmnElementField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public XmlQualifiedName sourceElement
    {
        get { return sourceElementField; }
        set { sourceElementField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public XmlQualifiedName targetElement
    {
        get { return targetElementField; }
        set { targetElementField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public MessageVisibleKind messageVisibleKind
    {
        get { return messageVisibleKindField; }
        set { messageVisibleKindField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool messageVisibleKindSpecified
    {
        get { return messageVisibleKindFieldSpecified; }
        set { messageVisibleKindFieldSpecified = value; }
    }
}