namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnIntermediateThrowEvent))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnImplicitThrowEvent))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnEndEvent))]
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("throwEvent", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public abstract partial class BpmnThrowEvent : BpmnEvent
{
    private BpmnDataInput[] dataInputField;

    private BpmnDataInputAssociation[] dataInputAssociationField;

    private BpmnInputSet inputSetField;

    private BpmnEventDefinition[] itemsField;

    private System.Xml.XmlQualifiedName[] eventDefinitionRefField;

    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("dataInput")]
    public BpmnDataInput[] dataInput
    {
        get { return dataInputField; }
        set { dataInputField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("dataInputAssociation")]
    public BpmnDataInputAssociation[] dataInputAssociation
    {
        get { return dataInputAssociationField; }
        set { dataInputAssociationField = value; }
    }

    /// <remarks/>
    public BpmnInputSet inputSet
    {
        get { return inputSetField; }
        set { inputSetField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("cancelEventDefinition", typeof(BpmnCancelEventDefinition))]
    [System.Xml.Serialization.XmlElementAttribute("compensateEventDefinition", typeof(BpmnCompensateEventDefinition))]
    [System.Xml.Serialization.XmlElementAttribute("conditionalEventDefinition", typeof(BpmnConditionalEventDefinition))]
    [System.Xml.Serialization.XmlElementAttribute("errorEventDefinition", typeof(BpmnErrorEventDefinition))]
    [System.Xml.Serialization.XmlElementAttribute("escalationEventDefinition", typeof(BpmnEscalationEventDefinition))]
    [System.Xml.Serialization.XmlElementAttribute("eventDefinition", typeof(BpmnEventDefinition))]
    [System.Xml.Serialization.XmlElementAttribute("linkEventDefinition", typeof(BpmnLinkEventDefinition))]
    [System.Xml.Serialization.XmlElementAttribute("messageEventDefinition", typeof(BpmnMessageEventDefinition))]
    [System.Xml.Serialization.XmlElementAttribute("signalEventDefinition", typeof(BpmnSignalEventDefinition))]
    [System.Xml.Serialization.XmlElementAttribute("terminateEventDefinition", typeof(BpmnTerminateEventDefinition))]
    [System.Xml.Serialization.XmlElementAttribute("timerEventDefinition", typeof(BpmnTimerEventDefinition))]
    public BpmnEventDefinition[] Items
    {
        get { return itemsField; }
        set { itemsField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("eventDefinitionRef")]
    public System.Xml.XmlQualifiedName[] eventDefinitionRef
    {
        get { return eventDefinitionRefField; }
        set { eventDefinitionRefField = value; }
    }
}