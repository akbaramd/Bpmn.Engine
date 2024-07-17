using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Serialization;

namespace Novin.Bpmn.Test.Models;

/// <remarks />
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[XmlRoot("complexGateway", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public class BpmnComplexGateway : BpmnGateway
{
    private BpmnExpression activationConditionField;

    private string defaultField;

    /// <remarks />
    public BpmnExpression activationCondition
    {
        get => activationConditionField;
        set => activationConditionField = value;
    }

    /// <remarks />
    [XmlAttribute(DataType = "IDREF")]
    public string @default
    {
        get => defaultField;
        set => defaultField = value;
    }
}