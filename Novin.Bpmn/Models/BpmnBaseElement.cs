#nullable disable
namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnRelationship))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnResourceParameter))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnOperation))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnCorrelationPropertyRetrievalExpression))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnConversationLink))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnMessageFlowAssociation))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnConversationAssociation))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnConversationNode))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnSubConversation))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnConversation))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnCallConversation))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnMessageFlow))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnParticipantMultiplicity))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnParticipant))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnCorrelationPropertyBinding))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnCorrelationSubscription))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnParticipantAssociation))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnCorrelationKey))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnRendering))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnInputOutputBinding))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnRootElement))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnSignal))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnResource))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnPartnerRole))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnPartnerEntity))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnMessage))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnItemDefinition))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnInterface))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnEscalation))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnError))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnEndPoint))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnDataStore))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnCorrelationProperty))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnCollaboration))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnGlobalConversation))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnChoreography))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnGlobalChoreographyTask))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnCategory))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnEventDefinition))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnTimerEventDefinition))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnTerminateEventDefinition))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnSignalEventDefinition))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnMessageEventDefinition))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnLinkEventDefinition))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnEscalationEventDefinition))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnErrorEventDefinition))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnConditionalEventDefinition))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnCompensateEventDefinition))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnCancelEventDefinition))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnCallableElement))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnProcess))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnGlobalTask))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnGlobalUserTask))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnGlobalScriptTask))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnGlobalManualTask))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnGlobalBusinessRuleTask))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnLane))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnLaneSet))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnLoopCharacteristics))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnStandardLoopCharacteristics))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnMultiInstanceLoopCharacteristics))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnResourceAssignmentExpression))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnResourceParameterBinding))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnResourceRole))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnPerformer))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnHumanPerformer))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnPotentialOwner))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnDataAssociation))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnDataOutputAssociation))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnDataInputAssociation))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnProperty))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnOutputSet))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnInputSet))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnDataOutput))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnDataInput))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnInputOutputSpecification))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnDataState))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnMonitoring))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnFlowElement))]
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
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnComplexBehaviorDefinition))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnCategoryValue))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnAuditing))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnAssignment))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnArtifact))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnTextAnnotation))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnGroup))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnAssociation))]
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("baseElement", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable=false)]
public abstract partial class BpmnBaseElement {
    
    private BpmnDocumentation[] documentationField;
    
    private BpmnExtensionElements extensionElementsField;
    
    private string idField;
    
    private System.Xml.XmlAttribute[] anyAttrField;
    
    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("documentation")]
    public BpmnDocumentation[] documentation {
        get {
            return documentationField;
        }
        set {
            documentationField = value;
        }
    }
    
    /// <remarks/>
    public BpmnExtensionElements extensionElements {
        get {
            return extensionElementsField;
        }
        set {
            extensionElementsField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute(DataType="ID")]
    public string id {
        get {
            return idField;
        }
        set {
            idField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAnyAttributeAttribute()]
    public System.Xml.XmlAttribute[] AnyAttr {
        get {
            return anyAttrField;
        }
        set {
            anyAttrField = value;
        }
    }
}