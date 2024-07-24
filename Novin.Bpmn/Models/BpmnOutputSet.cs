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
[XmlRoot("outputSet", Namespace = "http://www.omg.org/spec/BPMN/20100524/MODEL",
    IsNullable = false)]
public class BpmnOutputSet : BpmnBaseElement
{
    private string[] dataOutputRefsField;

    private string[] optionalOutputRefsField;

    private string[] whileExecutingOutputRefsField;

    private string[] inputSetRefsField;

    private string nameField;

    /// <remarks/>
    [XmlElement("dataOutputRefs", DataType = "IDREF")]
    public string[] dataOutputRefs
    {
        get { return dataOutputRefsField; }
        set { dataOutputRefsField = value; }
    }

    /// <remarks/>
    [XmlElement("optionalOutputRefs", DataType = "IDREF")]
    public string[] optionalOutputRefs
    {
        get { return optionalOutputRefsField; }
        set { optionalOutputRefsField = value; }
    }

    /// <remarks/>
    [XmlElement("whileExecutingOutputRefs", DataType = "IDREF")]
    public string[] whileExecutingOutputRefs
    {
        get { return whileExecutingOutputRefsField; }
        set { whileExecutingOutputRefsField = value; }
    }

    /// <remarks/>
    [XmlElement("inputSetRefs", DataType = "IDREF")]
    public string[] inputSetRefs
    {
        get { return inputSetRefsField; }
        set { inputSetRefsField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string name
    {
        get { return nameField; }
        set { nameField = value; }
    }
}