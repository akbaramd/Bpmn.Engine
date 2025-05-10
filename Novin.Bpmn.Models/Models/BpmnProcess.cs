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
[XmlRoot("process", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public class BpmnProcess : BpmnCallableElement
{
    private BpmnAuditing auditingField;

    private BpmnMonitoring monitoringField;

    private BpmnProperty[] propertyField;

    private BpmnLaneSet[] laneSetField;

    private BpmnFlowElement?[] itemsField;

    private BpmnArtifact[] items1Field;

    private BpmnResourceRole[] items2Field;

    private BpmnCorrelationSubscription[] correlationSubscriptionField;

    private XmlQualifiedName[] supportsField;

    private BpmnProcessType processTypeField;

    private bool isClosedField;

    private bool isExecutableField;

    private bool isExecutableFieldSpecified;

    private XmlQualifiedName definitionalCollaborationRefField;

    public BpmnProcess()
    {
        processTypeField = BpmnProcessType.None;
        isClosedField = false;
    }

    /// <remarks/>
    public BpmnAuditing auditing
    {
        get { return auditingField; }
        set { auditingField = value; }
    }

    /// <remarks/>
    public BpmnMonitoring monitoring
    {
        get { return monitoringField; }
        set { monitoringField = value; }
    }

    /// <remarks/>
    [XmlElement("property")]
    public BpmnProperty[] property
    {
        get { return propertyField; }
        set { propertyField = value; }
    }

    /// <remarks/>
    [XmlElement("laneSet")]
    public BpmnLaneSet[] laneSet
    {
        get { return laneSetField; }
        set { laneSetField = value; }
    }

    /// <remarks/>
    [XmlElement("adHocSubProcess", typeof(BpmnAdHocSubProcess))]
    [XmlElement("boundaryEvent", typeof(BpmnBoundaryEvent))]
    [XmlElement("businessRuleTask", typeof(BpmnBusinessRuleTask))]
    [XmlElement("callActivity", typeof(BpmnCallActivity))]
    [XmlElement("callChoreography", typeof(BpmnCallChoreography))]
    [XmlElement("choreographyTask", typeof(BpmnChoreographyTask))]
    [XmlElement("complexGateway", typeof(BpmnComplexGateway))]
    [XmlElement("dataObject", typeof(BpmnDataObject))]
    [XmlElement("dataObjectReference", typeof(BpmnDataObjectReference))]
    [XmlElement("dataStoreReference", typeof(BpmnDataStoreReference))]
    [XmlElement("endEvent", typeof(BpmnEndEvent))]
    [XmlElement("event", typeof(BpmnEvent))]
    [XmlElement("eventBasedGateway", typeof(BpmnEventBasedGateway))]
    [XmlElement("exclusiveGateway", typeof(BpmnExclusiveGateway))]
    [XmlElement("flowElement", typeof(BpmnFlowElement))]
    [XmlElement("implicitThrowEvent", typeof(BpmnImplicitThrowEvent))]
    [XmlElement("inclusiveGateway", typeof(BpmnInclusiveGateway))]
    [XmlElement("intermediateCatchEvent", typeof(BpmnIntermediateCatchEvent))]
    [XmlElement("intermediateThrowEvent", typeof(BpmnIntermediateThrowEvent))]
    [XmlElement("manualTask", typeof(BpmnManualTask))]
    [XmlElement("parallelGateway", typeof(BpmnParallelGateway))]
    [XmlElement("receiveTask", typeof(BpmnReceiveTask))]
    [XmlElement("scriptTask", typeof(BpmnScriptTask))]
    [XmlElement("sendTask", typeof(BpmnSendTask))]
    [XmlElement("sequenceFlow", typeof(BpmnSequenceFlow))]
    [XmlElement("serviceTask", typeof(BpmnServiceTask))]
    [XmlElement("startEvent", typeof(BpmnStartEvent))]
    [XmlElement("subChoreography", typeof(BpmnSubChoreography))]
    [XmlElement("subProcess", typeof(BpmnSubProcess))]
    [XmlElement("task", typeof(BpmnTask))]
    [XmlElement("transaction", typeof(BpmnTransaction))]
    [XmlElement("userTask", typeof(BpmnUserTask))]
    public BpmnFlowElement?[] Items
    {
        get { return itemsField; }
        set { itemsField = value; }
    }

    /// <remarks/>
    [XmlElement("artifact", typeof(BpmnArtifact))]
    [XmlElement("association", typeof(BpmnAssociation))]
    [XmlElement("group", typeof(BpmnGroup))]
    [XmlElement("textAnnotation", typeof(BpmnTextAnnotation))]
    public BpmnArtifact[] Items1
    {
        get { return items1Field; }
        set { items1Field = value; }
    }

    /// <remarks/>
    [XmlElement("performer", typeof(BpmnPerformer))]
    [XmlElement("resourceRole", typeof(BpmnResourceRole))]
    public BpmnResourceRole[] Items2
    {
        get { return items2Field; }
        set { items2Field = value; }
    }

    /// <remarks/>
    [XmlElement("correlationSubscription")]
    public BpmnCorrelationSubscription[] correlationSubscription
    {
        get { return correlationSubscriptionField; }
        set { correlationSubscriptionField = value; }
    }

    /// <remarks/>
    [XmlElement("supports")]
    public XmlQualifiedName[] supports
    {
        get { return supportsField; }
        set { supportsField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    [DefaultValue(BpmnProcessType.None)]
    public BpmnProcessType processType
    {
        get { return processTypeField; }
        set { processTypeField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    [DefaultValue(false)]
    public bool isClosed
    {
        get { return isClosedField; }
        set { isClosedField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public bool isExecutable
    {
        get { return isExecutableField; }
        set { isExecutableField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool isExecutableSpecified
    {
        get { return isExecutableFieldSpecified; }
        set { isExecutableFieldSpecified = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public XmlQualifiedName definitionalCollaborationRef
    {
        get { return definitionalCollaborationRefField; }
        set { definitionalCollaborationRefField = value; }
    }
}