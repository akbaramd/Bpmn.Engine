using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Serialization;

namespace Novin.Bpmn.Models;

/// <remarks />
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlRoot("eventBasedGateway",
    Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public class BpmnEventBasedGateway : BpmnGateway
{
    private BpmnEventBasedGatewayType eventGatewayTypeField;
    private bool instantiateField;

    public BpmnEventBasedGateway()
    {
        instantiateField = false;
        eventGatewayTypeField = BpmnEventBasedGatewayType.Exclusive;
    }

    /// <remarks />
    [XmlAttribute]
    [DefaultValue(false)]
    public bool instantiate
    {
        get => instantiateField;
        set => instantiateField = value;
    }

    /// <remarks />
    [XmlAttribute]
    [DefaultValue(BpmnEventBasedGatewayType.Exclusive)]
    public BpmnEventBasedGatewayType eventGatewayType
    {
        get => eventGatewayTypeField;
        set => eventGatewayTypeField = value;
    }
}