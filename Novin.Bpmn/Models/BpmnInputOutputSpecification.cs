#nullable disable
namespace Novin.Bpmn.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("ioSpecification", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public partial class BpmnInputOutputSpecification : BpmnBaseElement
{
    private BpmnDataInput[] dataInputField;

    private BpmnDataOutput[] dataOutputField;

    private BpmnInputSet[] inputSetField;

    private BpmnOutputSet[] outputSetField;

    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("dataInput")]
    public BpmnDataInput[] dataInput
    {
        get { return dataInputField; }
        set { dataInputField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("dataOutput")]
    public BpmnDataOutput[] dataOutput
    {
        get { return dataOutputField; }
        set { dataOutputField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("inputSet")]
    public BpmnInputSet[] inputSet
    {
        get { return inputSetField; }
        set { inputSetField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("outputSet")]
    public BpmnOutputSet[] outputSet
    {
        get { return outputSetField; }
        set { outputSetField = value; }
    }
}