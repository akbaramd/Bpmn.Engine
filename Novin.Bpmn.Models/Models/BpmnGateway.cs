using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Serialization;

namespace Novin.Bpmn.Models;

/// <remarks/>
[XmlInclude(typeof(BpmnParallelGateway))]
[XmlInclude(typeof(BpmnInclusiveGateway))]
[XmlInclude(typeof(BpmnExclusiveGateway))]
[XmlInclude(typeof(BpmnEventBasedGateway))]
[XmlInclude(typeof(BpmnComplexGateway))]
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
public class BpmnGateway : BpmnFlowNode
{
    private BpmnGatewayDirection gatewayDirectionField;

    public BpmnGateway()
    {
        gatewayDirectionField = BpmnGatewayDirection.Unspecified;
    }

    /// <remarks/>
    [XmlAttribute]
    [DefaultValue(BpmnGatewayDirection.Unspecified)]
    public BpmnGatewayDirection gatewayDirection
    {
        get { return gatewayDirectionField; }
        set { gatewayDirectionField = value; }
    }
}