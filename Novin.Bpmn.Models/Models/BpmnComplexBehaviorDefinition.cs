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
[XmlRoot("complexBehaviorDefinition",
    Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL", IsNullable = false)]
public class BpmnComplexBehaviorDefinition : BpmnBaseElement
{
    private BpmnFormalExpression conditionField;

    private BpmnImplicitThrowEvent eventField;

    /// <remarks/>
    public BpmnFormalExpression condition
    {
        get { return conditionField; }
        set { conditionField = value; }
    }

    /// <remarks/>
    public BpmnImplicitThrowEvent @event
    {
        get { return eventField; }
        set { eventField = value; }
    }
}