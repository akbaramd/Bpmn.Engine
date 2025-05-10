using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Serialization;

namespace Novin.Bpmn.Models;

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlRoot("standardLoopCharacteristics",
    Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public class BpmnStandardLoopCharacteristics : BpmnLoopCharacteristics
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
    [XmlAttribute]
    [DefaultValue(false)]
    public bool testBefore
    {
        get { return testBeforeField; }
        set { testBeforeField = value; }
    }

    /// <remarks/>
    [XmlAttribute(DataType = "integer")]
    public string loopMaximum
    {
        get { return loopMaximumField; }
        set { loopMaximumField = value; }
    }
}