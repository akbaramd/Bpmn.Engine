using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml;
using System.Xml.Serialization;

namespace Novin.Bpmn.Models;

/// <remarks/>
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
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlRoot("flowElement", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public abstract class BpmnFlowElement : BpmnBaseElement
{
    private BpmnAuditing auditingField;

    private BpmnMonitoring monitoringField;

    private XmlQualifiedName[] categoryValueRefField;

    private string nameField;

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
    [XmlElement("categoryValueRef")]
    public XmlQualifiedName[] categoryValueRef
    {
        get { return categoryValueRefField; }
        set { categoryValueRefField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string name
    {
        get { return nameField; }
        set { nameField = value; }
    }
}