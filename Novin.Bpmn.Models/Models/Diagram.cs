using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Serialization;

namespace Novin.Bpmn.Models;

/// <remarks/>
[XmlInclude(typeof(BPMNDiagram))]
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.omg.org/spec/DD/20100524/DI")]
[XmlRoot(Namespace = "http://www.omg.org/spec/DD/20100524/DI", IsNullable = false)]
public abstract class Diagram
{
    private string nameField;

    private string documentationField;

    private double resolutionField;

    private bool resolutionFieldSpecified;

    private string idField;

    /// <remarks/>
    [XmlAttribute]
    public string name
    {
        get { return nameField; }
        set { nameField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string documentation
    {
        get { return documentationField; }
        set { documentationField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public double resolution
    {
        get { return resolutionField; }
        set { resolutionField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool resolutionSpecified
    {
        get { return resolutionFieldSpecified; }
        set { resolutionFieldSpecified = value; }
    }

    /// <remarks/>
    [XmlAttribute(DataType = "ID")]
    public string id
    {
        get { return idField; }
        set { idField = value; }
    }
}