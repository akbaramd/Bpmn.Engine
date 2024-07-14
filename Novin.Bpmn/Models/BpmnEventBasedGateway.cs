namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("eventBasedGateway", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable=false)]
public partial class BpmnEventBasedGateway : BpmnGateway {
    
    private bool instantiateField;
    
    private BpmnEventBasedGatewayType eventGatewayTypeField;
    
    public BpmnEventBasedGateway() {
        instantiateField = false;
        eventGatewayTypeField = BpmnEventBasedGatewayType.Exclusive;
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    [System.ComponentModel.DefaultValueAttribute(false)]
    public bool instantiate {
        get {
            return instantiateField;
        }
        set {
            instantiateField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    [System.ComponentModel.DefaultValueAttribute(BpmnEventBasedGatewayType.Exclusive)]
    public BpmnEventBasedGatewayType eventGatewayType {
        get {
            return eventGatewayTypeField;
        }
        set {
            eventGatewayTypeField = value;
        }
    }
}