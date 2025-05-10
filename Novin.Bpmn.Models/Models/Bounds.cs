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
[XmlType(Namespace = "http://www.omg.org/spec/DD/20100524/DC")]
[XmlRoot(Namespace = "http://www.omg.org/spec/DD/20100524/DC", IsNullable = false)]
public class Bounds
{
    private double xField;

    private double yField;

    private double widthField;

    private double heightField;

    /// <remarks/>
    [XmlAttribute]
    public double x
    {
        get { return xField; }
        set { xField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public double y
    {
        get { return yField; }
        set { yField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public double width
    {
        get { return widthField; }
        set { widthField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public double height
    {
        get { return heightField; }
        set { heightField = value; }
    }
}