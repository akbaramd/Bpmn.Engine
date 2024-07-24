#nullable disable
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
[XmlRoot("ioSpecification", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public class BpmnInputOutputSpecification : BpmnBaseElement
{
    private BpmnDataInput[] dataInputField;

    private BpmnDataOutput[] dataOutputField;

    private BpmnInputSet[] inputSetField;

    private BpmnOutputSet[] outputSetField;

    /// <remarks/>
    [XmlElement("dataInput")]
    public BpmnDataInput[] dataInput
    {
        get { return dataInputField; }
        set { dataInputField = value; }
    }

    /// <remarks/>
    [XmlElement("dataOutput")]
    public BpmnDataOutput[] dataOutput
    {
        get { return dataOutputField; }
        set { dataOutputField = value; }
    }

    /// <remarks/>
    [XmlElement("inputSet")]
    public BpmnInputSet[] inputSet
    {
        get { return inputSetField; }
        set { inputSetField = value; }
    }

    /// <remarks/>
    [XmlElement("outputSet")]
    public BpmnOutputSet[] outputSet
    {
        get { return outputSetField; }
        set { outputSetField = value; }
    }
}