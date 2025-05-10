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
[XmlRoot("adHocSubProcess", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public class BpmnAdHocSubProcess : BpmnSubProcess
{
    private BpmnExpression completionConditionField;

    private bool cancelRemainingInstancesField;

    private BpmnAdHocOrdering orderingField;

    private bool orderingFieldSpecified;

    public BpmnAdHocSubProcess()
    {
        cancelRemainingInstancesField = true;
    }

    /// <remarks/>
    public BpmnExpression completionCondition
    {
        get { return completionConditionField; }
        set { completionConditionField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    [DefaultValue(true)]
    public bool cancelRemainingInstances
    {
        get { return cancelRemainingInstancesField; }
        set { cancelRemainingInstancesField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public BpmnAdHocOrdering ordering
    {
        get { return orderingField; }
        set { orderingField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool orderingSpecified
    {
        get { return orderingFieldSpecified; }
        set { orderingFieldSpecified = value; }
    }
}