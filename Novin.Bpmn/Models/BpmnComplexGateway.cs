﻿namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("complexGateway", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable=false)]
public partial class BpmnComplexGateway : BpmnGateway {
    
    private BpmnExpression activationConditionField;
    
    private string defaultField;
    
    /// <remarks/>
    public BpmnExpression activationCondition {
        get {
            return activationConditionField;
        }
        set {
            activationConditionField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute(DataType="IDREF")]
    public string @default {
        get {
            return defaultField;
        }
        set {
            defaultField = value;
        }
    }
}