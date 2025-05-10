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
[XmlRoot("conditionalEventDefinition",
    Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public class BpmnConditionalEventDefinition : BpmnEventDefinition
{
    private BpmnExpression conditionField;

    /// <remarks/>
    public BpmnExpression condition
    {
        get { return conditionField; }
        set { conditionField = value; }
    }
}