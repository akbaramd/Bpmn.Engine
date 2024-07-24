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
public class BPMNShape : LabeledShape
{
    private BPMNLabel bPMNLabelField;

    private XmlQualifiedName bpmnElementField;

    private bool isHorizontalField;

    private bool isHorizontalFieldSpecified;

    private bool isExpandedField;

    private bool isExpandedFieldSpecified;

    private bool isMarkerVisibleField;

    private bool isMarkerVisibleFieldSpecified;

    private bool isMessageVisibleField;

    private bool isMessageVisibleFieldSpecified;

    private ParticipantBandKind participantBandKindField;

    private bool participantBandKindFieldSpecified;

    private XmlQualifiedName choreographyActivityShapeField;

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
    public bool isHorizontal
    {
        get { return isHorizontalField; }
        set { isHorizontalField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool isHorizontalSpecified
    {
        get { return isHorizontalFieldSpecified; }
        set { isHorizontalFieldSpecified = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public bool isExpanded
    {
        get { return isExpandedField; }
        set { isExpandedField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool isExpandedSpecified
    {
        get { return isExpandedFieldSpecified; }
        set { isExpandedFieldSpecified = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public bool isMarkerVisible
    {
        get { return isMarkerVisibleField; }
        set { isMarkerVisibleField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool isMarkerVisibleSpecified
    {
        get { return isMarkerVisibleFieldSpecified; }
        set { isMarkerVisibleFieldSpecified = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public bool isMessageVisible
    {
        get { return isMessageVisibleField; }
        set { isMessageVisibleField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool isMessageVisibleSpecified
    {
        get { return isMessageVisibleFieldSpecified; }
        set { isMessageVisibleFieldSpecified = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public ParticipantBandKind participantBandKind
    {
        get { return participantBandKindField; }
        set { participantBandKindField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool participantBandKindSpecified
    {
        get { return participantBandKindFieldSpecified; }
        set { participantBandKindFieldSpecified = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public XmlQualifiedName choreographyActivityShape
    {
        get { return choreographyActivityShapeField; }
        set { choreographyActivityShapeField = value; }
    }
}