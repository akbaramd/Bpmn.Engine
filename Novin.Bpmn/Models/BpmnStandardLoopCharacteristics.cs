namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("standardLoopCharacteristics",
    Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public partial class BpmnStandardLoopCharacteristics : BpmnLoopCharacteristics
{
    private BpmnExpression loopConditionField;

    private bool testBeforeField;

    private string loopMaximumField;

    public BpmnStandardLoopCharacteristics()
    {
        testBeforeField = false;
    }

    /// <remarks/>
    public BpmnExpression loopCondition
    {
        get { return loopConditionField; }
        set { loopConditionField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    [System.ComponentModel.DefaultValueAttribute(false)]
    public bool testBefore
    {
        get { return testBeforeField; }
        set { testBeforeField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute(DataType = "integer")]
    public string loopMaximum
    {
        get { return loopMaximumField; }
        set { loopMaximumField = value; }
    }
}