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
[XmlRoot("inputSet", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public class BpmnInputSet : BpmnBaseElement
{
    private string[] dataInputRefsField;

    private string[] optionalInputRefsField;

    private string[] whileExecutingInputRefsField;

    private string[] outputSetRefsField;

    private string nameField;

    /// <remarks/>
    [XmlElement("dataInputRefs", DataType = "IDREF")]
    public string[] dataInputRefs
    {
        get { return dataInputRefsField; }
        set { dataInputRefsField = value; }
    }

    /// <remarks/>
    [XmlElement("optionalInputRefs", DataType = "IDREF")]
    public string[] optionalInputRefs
    {
        get { return optionalInputRefsField; }
        set { optionalInputRefsField = value; }
    }

    /// <remarks/>
    [XmlElement("whileExecutingInputRefs", DataType = "IDREF")]
    public string[] whileExecutingInputRefs
    {
        get { return whileExecutingInputRefsField; }
        set { whileExecutingInputRefsField = value; }
    }

    /// <remarks/>
    [XmlElement("outputSetRefs", DataType = "IDREF")]
    public string[] outputSetRefs
    {
        get { return outputSetRefsField; }
        set { outputSetRefsField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string name
    {
        get { return nameField; }
        set { nameField = value; }
    }
}