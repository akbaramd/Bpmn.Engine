namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnTransaction))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnAdHocSubProcess))]
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("subProcess", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public partial class BpmnSubProcess : BpmnActivity
{
    private BpmnLaneSet[] laneSetField;

    private BpmnFlowElement[] items1Field;

    private BpmnArtifact[] items2Field;

    private bool triggeredByEventField;

    public BpmnSubProcess()
    {
        triggeredByEventField = false;
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
    public BpmnFlowElement[] Items1
    {
        get { return items1Field; }
        set { items1Field = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("artifact", typeof(BpmnArtifact))]
    [System.Xml.Serialization.XmlElementAttribute("association", typeof(BpmnAssociation))]
    [System.Xml.Serialization.XmlElementAttribute("group", typeof(BpmnGroup))]
    [System.Xml.Serialization.XmlElementAttribute("textAnnotation", typeof(BpmnTextAnnotation))]
    public BpmnArtifact[] Items2
    {
        get { return items2Field; }
        set { items2Field = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    [System.ComponentModel.DefaultValueAttribute(false)]
    public bool triggeredByEvent
    {
        get { return triggeredByEventField; }
        set { triggeredByEventField = value; }
    }
}