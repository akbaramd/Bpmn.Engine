namespace Novin.Bpmn.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("subChoreography", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public partial class BpmnSubChoreography : BpmnChoreographyActivity
{
    private BpmnFlowElement[] itemsField;

    private BpmnArtifact[] items1Field;

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
    public BpmnFlowElement[] Items
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
}