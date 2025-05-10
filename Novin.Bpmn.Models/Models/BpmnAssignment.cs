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
[XmlRoot("assignment", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public class BpmnAssignment : BpmnBaseElement
{
    private BpmnExpression fromField;

    private BpmnExpression toField;

    /// <remarks/>
    public BpmnExpression from
    {
        get { return fromField; }
        set { fromField = value; }
    }

    /// <remarks/>
    public BpmnExpression to
    {
        get { return toField; }
        set { toField = value; }
    }
}