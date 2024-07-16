namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.omg.org/spec/BPMN/20100524/DI")]
[System.Xml.Serialization.XmlRootAttribute(Namespace = "http://www.omg.org/spec/BPMN/20100524/DI", IsNullable = false)]
public partial class BPMNShape : LabeledShape
{
    private BPMNLabel bPMNLabelField;

    private System.Xml.XmlQualifiedName bpmnElementField;

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

    private System.Xml.XmlQualifiedName choreographyActivityShapeField;

    /// <remarks/>
    public BPMNLabel BPMNLabel
    {
        get { return bPMNLabelField; }
        set { bPMNLabelField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public System.Xml.XmlQualifiedName bpmnElement
    {
        get { return bpmnElementField; }
        set { bpmnElementField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public bool isHorizontal
    {
        get { return isHorizontalField; }
        set { isHorizontalField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlIgnoreAttribute()]
    public bool isHorizontalSpecified
    {
        get { return isHorizontalFieldSpecified; }
        set { isHorizontalFieldSpecified = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public bool isExpanded
    {
        get { return isExpandedField; }
        set { isExpandedField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlIgnoreAttribute()]
    public bool isExpandedSpecified
    {
        get { return isExpandedFieldSpecified; }
        set { isExpandedFieldSpecified = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public bool isMarkerVisible
    {
        get { return isMarkerVisibleField; }
        set { isMarkerVisibleField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlIgnoreAttribute()]
    public bool isMarkerVisibleSpecified
    {
        get { return isMarkerVisibleFieldSpecified; }
        set { isMarkerVisibleFieldSpecified = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public bool isMessageVisible
    {
        get { return isMessageVisibleField; }
        set { isMessageVisibleField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlIgnoreAttribute()]
    public bool isMessageVisibleSpecified
    {
        get { return isMessageVisibleFieldSpecified; }
        set { isMessageVisibleFieldSpecified = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public ParticipantBandKind participantBandKind
    {
        get { return participantBandKindField; }
        set { participantBandKindField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlIgnoreAttribute()]
    public bool participantBandKindSpecified
    {
        get { return participantBandKindFieldSpecified; }
        set { participantBandKindFieldSpecified = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public System.Xml.XmlQualifiedName choreographyActivityShape
    {
        get { return choreographyActivityShapeField; }
        set { choreographyActivityShapeField = value; }
    }
}