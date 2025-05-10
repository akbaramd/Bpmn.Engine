using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml;
using System.Xml.Serialization;

namespace Novin.Bpmn.Models;

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlRoot("multiInstanceLoopCharacteristics",
    Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public class BpmnMultiInstanceLoopCharacteristics : BpmnLoopCharacteristics
{
    private BpmnExpression loopCardinalityField;

    private XmlQualifiedName loopDataInputRefField;

    private XmlQualifiedName loopDataOutputRefField;

    private BpmnDataInput inputDataItemField;

    private BpmnDataOutput outputDataItemField;

    private BpmnComplexBehaviorDefinition[] complexBehaviorDefinitionField;

    private BpmnExpression completionConditionField;

    private bool isSequentialField;

    private BpmnMultiInstanceFlowCondition behaviorField;

    private XmlQualifiedName oneBehaviorEventRefField;

    private XmlQualifiedName noneBehaviorEventRefField;

    public BpmnMultiInstanceLoopCharacteristics()
    {
        isSequentialField = false;
        behaviorField = BpmnMultiInstanceFlowCondition.All;
    }

    /// <remarks/>
    public BpmnExpression loopCardinality
    {
        get { return loopCardinalityField; }
        set { loopCardinalityField = value; }
    }

    /// <remarks/>
    public XmlQualifiedName loopDataInputRef
    {
        get { return loopDataInputRefField; }
        set { loopDataInputRefField = value; }
    }

    /// <remarks/>
    public XmlQualifiedName loopDataOutputRef
    {
        get { return loopDataOutputRefField; }
        set { loopDataOutputRefField = value; }
    }

    /// <remarks/>
    public BpmnDataInput inputDataItem
    {
        get { return inputDataItemField; }
        set { inputDataItemField = value; }
    }

    /// <remarks/>
    public BpmnDataOutput outputDataItem
    {
        get { return outputDataItemField; }
        set { outputDataItemField = value; }
    }

    /// <remarks/>
    [XmlElement("complexBehaviorDefinition")]
    public BpmnComplexBehaviorDefinition[] complexBehaviorDefinition
    {
        get { return complexBehaviorDefinitionField; }
        set { complexBehaviorDefinitionField = value; }
    }

    /// <remarks/>
    public BpmnExpression completionCondition
    {
        get { return completionConditionField; }
        set { completionConditionField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    [DefaultValue(false)]
    public bool isSequential
    {
        get { return isSequentialField; }
        set { isSequentialField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    [DefaultValue(BpmnMultiInstanceFlowCondition.All)]
    public BpmnMultiInstanceFlowCondition behavior
    {
        get { return behaviorField; }
        set { behaviorField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public XmlQualifiedName oneBehaviorEventRef
    {
        get { return oneBehaviorEventRefField; }
        set { oneBehaviorEventRefField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public XmlQualifiedName noneBehaviorEventRef
    {
        get { return noneBehaviorEventRefField; }
        set { noneBehaviorEventRefField = value; }
    }
}