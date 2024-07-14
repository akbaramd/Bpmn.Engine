namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("multiInstanceLoopCharacteristics", Namespace="http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable=false)]
public partial class BpmnMultiInstanceLoopCharacteristics : BpmnLoopCharacteristics {
    
    private BpmnExpression loopCardinalityField;
    
    private System.Xml.XmlQualifiedName loopDataInputRefField;
    
    private System.Xml.XmlQualifiedName loopDataOutputRefField;
    
    private BpmnDataInput inputDataItemField;
    
    private BpmnDataOutput outputDataItemField;
    
    private BpmnComplexBehaviorDefinition[] complexBehaviorDefinitionField;
    
    private BpmnExpression completionConditionField;
    
    private bool isSequentialField;
    
    private BpmnMultiInstanceFlowCondition behaviorField;
    
    private System.Xml.XmlQualifiedName oneBehaviorEventRefField;
    
    private System.Xml.XmlQualifiedName noneBehaviorEventRefField;
    
    public BpmnMultiInstanceLoopCharacteristics() {
        isSequentialField = false;
        behaviorField = BpmnMultiInstanceFlowCondition.All;
    }
    
    /// <remarks/>
    public BpmnExpression loopCardinality {
        get {
            return loopCardinalityField;
        }
        set {
            loopCardinalityField = value;
        }
    }
    
    /// <remarks/>
    public System.Xml.XmlQualifiedName loopDataInputRef {
        get {
            return loopDataInputRefField;
        }
        set {
            loopDataInputRefField = value;
        }
    }
    
    /// <remarks/>
    public System.Xml.XmlQualifiedName loopDataOutputRef {
        get {
            return loopDataOutputRefField;
        }
        set {
            loopDataOutputRefField = value;
        }
    }
    
    /// <remarks/>
    public BpmnDataInput inputDataItem {
        get {
            return inputDataItemField;
        }
        set {
            inputDataItemField = value;
        }
    }
    
    /// <remarks/>
    public BpmnDataOutput outputDataItem {
        get {
            return outputDataItemField;
        }
        set {
            outputDataItemField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("complexBehaviorDefinition")]
    public BpmnComplexBehaviorDefinition[] complexBehaviorDefinition {
        get {
            return complexBehaviorDefinitionField;
        }
        set {
            complexBehaviorDefinitionField = value;
        }
    }
    
    /// <remarks/>
    public BpmnExpression completionCondition {
        get {
            return completionConditionField;
        }
        set {
            completionConditionField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    [System.ComponentModel.DefaultValueAttribute(false)]
    public bool isSequential {
        get {
            return isSequentialField;
        }
        set {
            isSequentialField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    [System.ComponentModel.DefaultValueAttribute(BpmnMultiInstanceFlowCondition.All)]
    public BpmnMultiInstanceFlowCondition behavior {
        get {
            return behaviorField;
        }
        set {
            behaviorField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public System.Xml.XmlQualifiedName oneBehaviorEventRef {
        get {
            return oneBehaviorEventRefField;
        }
        set {
            oneBehaviorEventRefField = value;
        }
    }
    
    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public System.Xml.XmlQualifiedName noneBehaviorEventRef {
        get {
            return noneBehaviorEventRefField;
        }
        set {
            noneBehaviorEventRefField = value;
        }
    }
}