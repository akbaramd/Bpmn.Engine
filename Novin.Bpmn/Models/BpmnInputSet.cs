namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL")]
[System.Xml.Serialization.XmlRootAttribute("inputSet", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public partial class BpmnInputSet : BpmnBaseElement
{
    private string[] dataInputRefsField;

    private string[] optionalInputRefsField;

    private string[] whileExecutingInputRefsField;

    private string[] outputSetRefsField;

    private string nameField;

    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("dataInputRefs", DataType = "IDREF")]
    public string[] dataInputRefs
    {
        get { return dataInputRefsField; }
        set { dataInputRefsField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("optionalInputRefs", DataType = "IDREF")]
    public string[] optionalInputRefs
    {
        get { return optionalInputRefsField; }
        set { optionalInputRefsField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("whileExecutingInputRefs", DataType = "IDREF")]
    public string[] whileExecutingInputRefs
    {
        get { return whileExecutingInputRefsField; }
        set { whileExecutingInputRefsField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlElementAttribute("outputSetRefs", DataType = "IDREF")]
    public string[] outputSetRefs
    {
        get { return outputSetRefsField; }
        set { outputSetRefsField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public string name
    {
        get { return nameField; }
        set { nameField = value; }
    }
}