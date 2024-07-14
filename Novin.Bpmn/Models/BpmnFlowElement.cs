namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnSequenceFlow))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnFlowNode))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnGateway))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnParallelGateway))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnInclusiveGateway))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnExclusiveGateway))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnEventBasedGateway))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnComplexGateway))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnChoreographyActivity))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnSubChoreography))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnChoreographyTask))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnCallChoreography))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnEvent))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnThrowEvent))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnIntermediateThrowEvent))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnImplicitThrowEvent))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnEndEvent))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnCatchEvent))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnStartEvent))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnIntermediateCatchEvent))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnBoundaryEvent))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnActivity))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnTask))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnUserTask))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnServiceTask))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnSendTask))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnScriptTask))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnReceiveTask))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnManualTask))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnBusinessRuleTask))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnSubProcess))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnTransaction))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnAdHocSubProcess))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnCallActivity))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnDataStoreReference))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnDataObjectReference))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnDataObject))]
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("flowElement", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable=false)]
public abstract partial class BpmnFlowElement : BpmnBaseElement {
    
    private BpmnAuditing auditingField;
    
    private BpmnMonitoring monitoringField;
    
    private System.Xml.XmlQualifiedName[] categoryValueRefField;
    
    private string nameField;
    
    /// <remarks/>
    public BpmnAuditing auditing {
        get {
            return auditingField;
        }
        set {
            auditingField = value;
        }
    }
    
    /// <remarks/>
    public BpmnMonitoring monitoring {
        get {
            return monitoringField;
        }
        set {
            monitoringField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("categoryValueRef")]
    public System.Xml.XmlQualifiedName[] categoryValueRef {
        get {
            return categoryValueRefField;
        }
        set {
            categoryValueRefField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public string name {
        get {
            return nameField;
        }
        set {
            nameField = value;
        }
    }
}