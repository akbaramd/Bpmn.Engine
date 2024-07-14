using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml;
using System.Xml.Serialization;

namespace Novin.Bpmn.Test.Models;

/// <remarks />
[XmlInclude(typeof(BpmnStartEvent))]
[XmlInclude(typeof(BpmnIntermediateCatchEvent))]
[XmlInclude(typeof(BpmnBoundaryEvent))]
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlRoot("catchEvent", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public abstract class BpmnCatchEvent : BpmnEvent
{
    private BpmnDataOutputAssociation[] dataOutputAssociationField;

    private BpmnDataOutput[] dataOutputField;

    private XmlQualifiedName[] eventDefinitionRefField;

    private BpmnEventDefinition[] itemsField;

    private BpmnOutputSet outputSetField;

    private bool parallelMultipleField;

    public BpmnCatchEvent()
    {
        parallelMultipleField = false;
    }

    /// <remarks />
    [XmlElement("dataOutput")]
    public BpmnDataOutput[] dataOutput
    {
        get => dataOutputField;
        set => dataOutputField = value;
    }

    /// <remarks />
    [XmlElement("dataOutputAssociation")]
    public BpmnDataOutputAssociation[] dataOutputAssociation
    {
        get => dataOutputAssociationField;
        set => dataOutputAssociationField = value;
    }

    /// <remarks />
    public BpmnOutputSet outputSet
    {
        get => outputSetField;
        set => outputSetField = value;
    }

    /// <remarks />
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
        get => itemsField;
        set => itemsField = value;
    }

    /// <remarks />
    [XmlElement("eventDefinitionRef")]
    public XmlQualifiedName[] eventDefinitionRef
    {
        get => eventDefinitionRefField;
        set => eventDefinitionRefField = value;
    }

    /// <remarks />
    [XmlAttribute]
    [DefaultValue(false)]
    public bool parallelMultiple
    {
        get => parallelMultipleField;
        set => parallelMultipleField = value;
    }
}