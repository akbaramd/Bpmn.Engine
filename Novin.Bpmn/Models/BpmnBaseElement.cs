﻿#nullable disable
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml;
using System.Xml.Serialization;

namespace Novin.Bpmn.Models;

/// <remarks/>
[XmlInclude(typeof(BpmnRelationship))]
[XmlInclude(typeof(BpmnResourceParameter))]
[XmlInclude(typeof(BpmnOperation))]
[XmlInclude(typeof(BpmnCorrelationPropertyRetrievalExpression))]
[XmlInclude(typeof(BpmnConversationLink))]
[XmlInclude(typeof(BpmnMessageFlowAssociation))]
[XmlInclude(typeof(BpmnConversationAssociation))]
[XmlInclude(typeof(BpmnConversationNode))]
[XmlInclude(typeof(BpmnSubConversation))]
[XmlInclude(typeof(BpmnConversation))]
[XmlInclude(typeof(BpmnCallConversation))]
[XmlInclude(typeof(BpmnMessageFlow))]
[XmlInclude(typeof(BpmnParticipantMultiplicity))]
[XmlInclude(typeof(BpmnParticipant))]
[XmlInclude(typeof(BpmnCorrelationPropertyBinding))]
[XmlInclude(typeof(BpmnCorrelationSubscription))]
[XmlInclude(typeof(BpmnParticipantAssociation))]
[XmlInclude(typeof(BpmnCorrelationKey))]
[XmlInclude(typeof(BpmnRendering))]
[XmlInclude(typeof(BpmnInputOutputBinding))]
[XmlInclude(typeof(BpmnRootElement))]
[XmlInclude(typeof(BpmnSignal))]
[XmlInclude(typeof(BpmnResource))]
[XmlInclude(typeof(BpmnPartnerRole))]
[XmlInclude(typeof(BpmnPartnerEntity))]
[XmlInclude(typeof(BpmnMessage))]
[XmlInclude(typeof(BpmnItemDefinition))]
[XmlInclude(typeof(BpmnInterface))]
[XmlInclude(typeof(BpmnEscalation))]
[XmlInclude(typeof(BpmnError))]
[XmlInclude(typeof(BpmnEndPoint))]
[XmlInclude(typeof(BpmnDataStore))]
[XmlInclude(typeof(BpmnCorrelationProperty))]
[XmlInclude(typeof(BpmnCollaboration))]
[XmlInclude(typeof(BpmnGlobalConversation))]
[XmlInclude(typeof(BpmnChoreography))]
[XmlInclude(typeof(BpmnGlobalChoreographyTask))]
[XmlInclude(typeof(BpmnCategory))]
[XmlInclude(typeof(BpmnEventDefinition))]
[XmlInclude(typeof(BpmnTimerEventDefinition))]
[XmlInclude(typeof(BpmnTerminateEventDefinition))]
[XmlInclude(typeof(BpmnSignalEventDefinition))]
[XmlInclude(typeof(BpmnMessageEventDefinition))]
[XmlInclude(typeof(BpmnLinkEventDefinition))]
[XmlInclude(typeof(BpmnEscalationEventDefinition))]
[XmlInclude(typeof(BpmnErrorEventDefinition))]
[XmlInclude(typeof(BpmnConditionalEventDefinition))]
[XmlInclude(typeof(BpmnCompensateEventDefinition))]
[XmlInclude(typeof(BpmnCancelEventDefinition))]
[XmlInclude(typeof(BpmnCallableElement))]
[XmlInclude(typeof(BpmnProcess))]
[XmlInclude(typeof(BpmnGlobalTask))]
[XmlInclude(typeof(BpmnGlobalUserTask))]
[XmlInclude(typeof(BpmnGlobalScriptTask))]
[XmlInclude(typeof(BpmnGlobalManualTask))]
[XmlInclude(typeof(BpmnGlobalBusinessRuleTask))]
[XmlInclude(typeof(BpmnLane))]
[XmlInclude(typeof(BpmnLaneSet))]
[XmlInclude(typeof(BpmnLoopCharacteristics))]
[XmlInclude(typeof(BpmnStandardLoopCharacteristics))]
[XmlInclude(typeof(BpmnMultiInstanceLoopCharacteristics))]
[XmlInclude(typeof(BpmnResourceAssignmentExpression))]
[XmlInclude(typeof(BpmnResourceParameterBinding))]
[XmlInclude(typeof(BpmnResourceRole))]
[XmlInclude(typeof(BpmnPerformer))]
[XmlInclude(typeof(BpmnHumanPerformer))]
[XmlInclude(typeof(BpmnPotentialOwner))]
[XmlInclude(typeof(BpmnDataAssociation))]
[XmlInclude(typeof(BpmnDataOutputAssociation))]
[XmlInclude(typeof(BpmnDataInputAssociation))]
[XmlInclude(typeof(BpmnProperty))]
[XmlInclude(typeof(BpmnOutputSet))]
[XmlInclude(typeof(BpmnInputSet))]
[XmlInclude(typeof(BpmnDataOutput))]
[XmlInclude(typeof(BpmnDataInput))]
[XmlInclude(typeof(BpmnInputOutputSpecification))]
[XmlInclude(typeof(BpmnDataState))]
[XmlInclude(typeof(BpmnMonitoring))]
[XmlInclude(typeof(BpmnFlowElement))]
[XmlInclude(typeof(BpmnSequenceFlow))]
[XmlInclude(typeof(BpmnFlowNode))]
[XmlInclude(typeof(BpmnGateway))]
[XmlInclude(typeof(BpmnParallelGateway))]
[XmlInclude(typeof(BpmnInclusiveGateway))]
[XmlInclude(typeof(BpmnExclusiveGateway))]
[XmlInclude(typeof(BpmnEventBasedGateway))]
[XmlInclude(typeof(BpmnComplexGateway))]
[XmlInclude(typeof(BpmnChoreographyActivity))]
[XmlInclude(typeof(BpmnSubChoreography))]
[XmlInclude(typeof(BpmnChoreographyTask))]
[XmlInclude(typeof(BpmnCallChoreography))]
[XmlInclude(typeof(BpmnEvent))]
[XmlInclude(typeof(BpmnThrowEvent))]
[XmlInclude(typeof(BpmnIntermediateThrowEvent))]
[XmlInclude(typeof(BpmnImplicitThrowEvent))]
[XmlInclude(typeof(BpmnEndEvent))]
[XmlInclude(typeof(BpmnCatchEvent))]
[XmlInclude(typeof(BpmnStartEvent))]
[XmlInclude(typeof(BpmnIntermediateCatchEvent))]
[XmlInclude(typeof(BpmnBoundaryEvent))]
[XmlInclude(typeof(BpmnActivity))]
[XmlInclude(typeof(BpmnTask))]
[XmlInclude(typeof(BpmnUserTask))]
[XmlInclude(typeof(BpmnServiceTask))]
[XmlInclude(typeof(BpmnSendTask))]
[XmlInclude(typeof(BpmnScriptTask))]
[XmlInclude(typeof(BpmnReceiveTask))]
[XmlInclude(typeof(BpmnManualTask))]
[XmlInclude(typeof(BpmnBusinessRuleTask))]
[XmlInclude(typeof(BpmnSubProcess))]
[XmlInclude(typeof(BpmnTransaction))]
[XmlInclude(typeof(BpmnAdHocSubProcess))]
[XmlInclude(typeof(BpmnCallActivity))]
[XmlInclude(typeof(BpmnDataStoreReference))]
[XmlInclude(typeof(BpmnDataObjectReference))]
[XmlInclude(typeof(BpmnDataObject))]
[XmlInclude(typeof(BpmnComplexBehaviorDefinition))]
[XmlInclude(typeof(BpmnCategoryValue))]
[XmlInclude(typeof(BpmnAuditing))]
[XmlInclude(typeof(BpmnAssignment))]
[XmlInclude(typeof(BpmnArtifact))]
[XmlInclude(typeof(BpmnTextAnnotation))]
[XmlInclude(typeof(BpmnGroup))]
[XmlInclude(typeof(BpmnAssociation))]
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlRoot("baseElement", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public abstract class BpmnBaseElement
{
    private BpmnDocumentation[] documentationField;

    private BpmnExtensionElements extensionElementsField;

    private string idField;

    private XmlAttribute[] anyAttrField;

    /// <remarks/>
    [XmlElement("documentation")]
    public BpmnDocumentation[] documentation
    {
        get { return documentationField; }
        set { documentationField = value; }
    }

    /// <remarks/>
    public BpmnExtensionElements extensionElements
    {
        get { return extensionElementsField; }
        set { extensionElementsField = value; }
    }

    /// <remarks/>
    [XmlAttribute(DataType = "ID")]
    public string id
    {
        get { return idField; }
        set { idField = value; }
    }

    /// <remarks/>
    [XmlAnyAttribute]
    public XmlAttribute[] AnyAttr
    {
        get { return anyAttrField; }
        set { anyAttrField = value; }
    }
}