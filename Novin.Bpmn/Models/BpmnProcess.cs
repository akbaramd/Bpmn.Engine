namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("process", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public partial class BpmnProcess : BpmnCallableElement
{
    private BpmnAuditing auditingField;

    private BpmnMonitoring monitoringField;

    private BpmnProperty[] propertyField;

    private BpmnLaneSet[] laneSetField;

    private BpmnFlowElement?[] itemsField;

    private BpmnArtifact[] items1Field;

    private BpmnResourceRole[] items2Field;

    private BpmnCorrelationSubscription[] correlationSubscriptionField;

    private System.Xml.XmlQualifiedName[] supportsField;

    private BpmnProcessType processTypeField;

    private bool isClosedField;

    private bool isExecutableField;

    private bool isExecutableFieldSpecified;

    private System.Xml.XmlQualifiedName definitionalCollaborationRefField;

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
    [System.Xml.Serialization.XmlElementAttribute("property")]
    public BpmnProperty[] property
    {
        get { return propertyField; }
        set { propertyField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("laneSet")]
    public BpmnLaneSet[] laneSet
    {
        get { return laneSetField; }
        set { laneSetField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("adHocSubProcess", typeof(BpmnAdHocSubProcess))]
    [System.Xml.Serialization.XmlElementAttribute("boundaryEvent", typeof(BpmnBoundaryEvent))]
    [System.Xml.Serialization.XmlElementAttribute("businessRuleTask", typeof(BpmnBusinessRuleTask))]
    [System.Xml.Serialization.XmlElementAttribute("callActivity", typeof(BpmnCallActivity))]
    [System.Xml.Serialization.XmlElementAttribute("callChoreography", typeof(BpmnCallChoreography))]
    [System.Xml.Serialization.XmlElementAttribute("choreographyTask", typeof(BpmnChoreographyTask))]
    [System.Xml.Serialization.XmlElementAttribute("complexGateway", typeof(BpmnComplexGateway))]
    [System.Xml.Serialization.XmlElementAttribute("dataObject", typeof(BpmnDataObject))]
    [System.Xml.Serialization.XmlElementAttribute("dataObjectReference", typeof(BpmnDataObjectReference))]
    [System.Xml.Serialization.XmlElementAttribute("dataStoreReference", typeof(BpmnDataStoreReference))]
    [System.Xml.Serialization.XmlElementAttribute("endEvent", typeof(BpmnEndEvent))]
    [System.Xml.Serialization.XmlElementAttribute("event", typeof(BpmnEvent))]
    [System.Xml.Serialization.XmlElementAttribute("eventBasedGateway", typeof(BpmnEventBasedGateway))]
    [System.Xml.Serialization.XmlElementAttribute("exclusiveGateway", typeof(BpmnExclusiveGateway))]
    [System.Xml.Serialization.XmlElementAttribute("flowElement", typeof(BpmnFlowElement))]
    [System.Xml.Serialization.XmlElementAttribute("implicitThrowEvent", typeof(BpmnImplicitThrowEvent))]
    [System.Xml.Serialization.XmlElementAttribute("inclusiveGateway", typeof(BpmnInclusiveGateway))]
    [System.Xml.Serialization.XmlElementAttribute("intermediateCatchEvent", typeof(BpmnIntermediateCatchEvent))]
    [System.Xml.Serialization.XmlElementAttribute("intermediateThrowEvent", typeof(BpmnIntermediateThrowEvent))]
    [System.Xml.Serialization.XmlElementAttribute("manualTask", typeof(BpmnManualTask))]
    [System.Xml.Serialization.XmlElementAttribute("parallelGateway", typeof(BpmnParallelGateway))]
    [System.Xml.Serialization.XmlElementAttribute("receiveTask", typeof(BpmnReceiveTask))]
    [System.Xml.Serialization.XmlElementAttribute("scriptTask", typeof(BpmnScriptTask))]
    [System.Xml.Serialization.XmlElementAttribute("sendTask", typeof(BpmnSendTask))]
    [System.Xml.Serialization.XmlElementAttribute("sequenceFlow", typeof(BpmnSequenceFlow))]
    [System.Xml.Serialization.XmlElementAttribute("serviceTask", typeof(BpmnServiceTask))]
    [System.Xml.Serialization.XmlElementAttribute("startEvent", typeof(BpmnStartEvent))]
    [System.Xml.Serialization.XmlElementAttribute("subChoreography", typeof(BpmnSubChoreography))]
    [System.Xml.Serialization.XmlElementAttribute("subProcess", typeof(BpmnSubProcess))]
    [System.Xml.Serialization.XmlElementAttribute("task", typeof(BpmnTask))]
    [System.Xml.Serialization.XmlElementAttribute("transaction", typeof(BpmnTransaction))]
    [System.Xml.Serialization.XmlElementAttribute("userTask", typeof(BpmnUserTask))]
    public BpmnFlowElement?[] Items
    {
        get { return itemsField; }
        set { itemsField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("artifact", typeof(BpmnArtifact))]
    [System.Xml.Serialization.XmlElementAttribute("association", typeof(BpmnAssociation))]
    [System.Xml.Serialization.XmlElementAttribute("group", typeof(BpmnGroup))]
    [System.Xml.Serialization.XmlElementAttribute("textAnnotation", typeof(BpmnTextAnnotation))]
    public BpmnArtifact[] Items1
    {
        get { return items1Field; }
        set { items1Field = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("performer", typeof(BpmnPerformer))]
    [System.Xml.Serialization.XmlElementAttribute("resourceRole", typeof(BpmnResourceRole))]
    public BpmnResourceRole[] Items2
    {
        get { return items2Field; }
        set { items2Field = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("correlationSubscription")]
    public BpmnCorrelationSubscription[] correlationSubscription
    {
        get { return correlationSubscriptionField; }
        set { correlationSubscriptionField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("supports")]
    public System.Xml.XmlQualifiedName[] supports
    {
        get { return supportsField; }
        set { supportsField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    [System.ComponentModel.DefaultValueAttribute(BpmnProcessType.None)]
    public BpmnProcessType processType
    {
        get { return processTypeField; }
        set { processTypeField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    [System.ComponentModel.DefaultValueAttribute(false)]
    public bool isClosed
    {
        get { return isClosedField; }
        set { isClosedField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public bool isExecutable
    {
        get { return isExecutableField; }
        set { isExecutableField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlIgnoreAttribute()]
    public bool isExecutableSpecified
    {
        get { return isExecutableFieldSpecified; }
        set { isExecutableFieldSpecified = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public System.Xml.XmlQualifiedName definitionalCollaborationRef
    {
        get { return definitionalCollaborationRefField; }
        set { definitionalCollaborationRefField = value; }
    }
}