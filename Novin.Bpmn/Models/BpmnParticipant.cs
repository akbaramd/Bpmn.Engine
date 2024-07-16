namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("participant", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public partial class BpmnParticipant : BpmnBaseElement
{
    private System.Xml.XmlQualifiedName[] interfaceRefField;

    private System.Xml.XmlQualifiedName[] endPointRefField;

    private BpmnParticipantMultiplicity participantMultiplicityField;

    private string nameField;

    private System.Xml.XmlQualifiedName processRefField;

    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("interfaceRef")]
    public System.Xml.XmlQualifiedName[] interfaceRef
    {
        get { return interfaceRefField; }
        set { interfaceRefField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("endPointRef")]
    public System.Xml.XmlQualifiedName[] endPointRef
    {
        get { return endPointRefField; }
        set { endPointRefField = value; }
    }

    /// <remarks/>
    public BpmnParticipantMultiplicity participantMultiplicity
    {
        get { return participantMultiplicityField; }
        set { participantMultiplicityField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public string name
    {
        get { return nameField; }
        set { nameField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public System.Xml.XmlQualifiedName processRef
    {
        get { return processRefField; }
        set { processRefField = value; }
    }
}