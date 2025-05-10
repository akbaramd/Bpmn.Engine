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
[XmlRoot("sequenceFlow", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public class BpmnSequenceFlow : BpmnFlowElement
{
    private BpmnExpression conditionExpressionField;

    private string sourceRefField;

    private string targetRefField;

    private bool isImmediateField;

    private bool isImmediateFieldSpecified;

    /// <remarks/>
    public BpmnExpression conditionExpression
    {
        get { return conditionExpressionField; }
        set { conditionExpressionField = value; }
    }

    /// <remarks/>
    [XmlAttribute(DataType = "IDREF")]
    public string sourceRef
    {
        get { return sourceRefField; }
        set { sourceRefField = value; }
    }

    /// <remarks/>
    [XmlAttribute(DataType = "IDREF")]
    public string targetRef
    {
        get { return targetRefField; }
        set { targetRefField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public bool isImmediate
    {
        get { return isImmediateField; }
        set { isImmediateField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool isImmediateSpecified
    {
        get { return isImmediateFieldSpecified; }
        set { isImmediateFieldSpecified = value; }
    }
}