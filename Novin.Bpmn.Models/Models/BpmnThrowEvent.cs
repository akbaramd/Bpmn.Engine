using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml;
using System.Xml.Serialization;

namespace Novin.Bpmn.Models;

/// <remarks/>
[XmlInclude(typeof(BpmnIntermediateThrowEvent))]
[XmlInclude(typeof(BpmnImplicitThrowEvent))]
[XmlInclude(typeof(BpmnEndEvent))]
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlRoot("throwEvent", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public abstract class BpmnThrowEvent : BpmnEvent
{
    private BpmnDataInput[] dataInputField;

    private BpmnDataInputAssociation[] dataInputAssociationField;

    private BpmnInputSet inputSetField;

    private BpmnEventDefinition[] itemsField;

    private XmlQualifiedName[] eventDefinitionRefField;

    /// <remarks/>
    [XmlElement("dataInput")]
    public BpmnDataInput[] dataInput
    {
        get { return dataInputField; }
        set { dataInputField = value; }
    }

    /// <remarks/>
    [XmlElement("dataInputAssociation")]
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
    [XmlElement("cancelEventDefinition", typeof(BpmnCancelEventDefinition))]
    [XmlElement("compensateEventDefinition", typeof(BpmnCompensateEventDefinition))]
    [XmlElement("conditionalEventDefinition", typeof(BpmnConditionalEventDefinition))]
    [XmlElement("errorEventDefinition", typeof(BpmnErrorEventDefinition))]
    [XmlElement("escalationEventDefinition", typeof(BpmnEscalationEventDefinition))]
    [XmlElement("eventDefinition", typeof(BpmnEventDefinition))]
    [XmlElement("linkEventDefinition", typeof(BpmnLinkEventDefinition))]
    [XmlElement("messageEventDefinition", typeof(BpmnMessageEventDefinition))]
    [XmlElement("signalEventDefinition", typeof(BpmnSignalEventDefinition))]
    [XmlElement("terminateEventDefinition", typeof(BpmnTerminateEventDefinition))]
    [XmlElement("timerEventDefinition", typeof(BpmnTimerEventDefinition))]
    public BpmnEventDefinition[] Items
    {
        get { return itemsField; }
        set { itemsField = value; }
    }

    /// <remarks/>
    [XmlElement("eventDefinitionRef")]
    public XmlQualifiedName[] eventDefinitionRef
    {
        get { return eventDefinitionRefField; }
        set { eventDefinitionRefField = value; }
    }
}