namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnParallelGateway))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnInclusiveGateway))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnExclusiveGateway))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnEventBasedGateway))]
[System.Xml.Serialization.XmlIncludeAttribute(typeof(BpmnComplexGateway))]
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public partial class BpmnGateway : BpmnFlowNode
{
    private BpmnGatewayDirection gatewayDirectionField;

    public BpmnGateway()
    {
        gatewayDirectionField = BpmnGatewayDirection.Unspecified;
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    [System.ComponentModel.DefaultValueAttribute(BpmnGatewayDirection.Unspecified)]
    public BpmnGatewayDirection gatewayDirection
    {
        get { return gatewayDirectionField; }
        set { gatewayDirectionField = value; }
    }
}