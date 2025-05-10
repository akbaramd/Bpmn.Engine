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
public class Font
{
    private string nameField;

    private double sizeField;

    private bool sizeFieldSpecified;

    private bool isBoldField;

    private bool isBoldFieldSpecified;

    private bool isItalicField;

    private bool isItalicFieldSpecified;

    private bool isUnderlineField;

    private bool isUnderlineFieldSpecified;

    private bool isStrikeThroughField;

    private bool isStrikeThroughFieldSpecified;

    /// <remarks/>
    [XmlAttribute]
    public string name
    {
        get { return nameField; }
        set { nameField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public double size
    {
        get { return sizeField; }
        set { sizeField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool sizeSpecified
    {
        get { return sizeFieldSpecified; }
        set { sizeFieldSpecified = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public bool isBold
    {
        get { return isBoldField; }
        set { isBoldField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool isBoldSpecified
    {
        get { return isBoldFieldSpecified; }
        set { isBoldFieldSpecified = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public bool isItalic
    {
        get { return isItalicField; }
        set { isItalicField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool isItalicSpecified
    {
        get { return isItalicFieldSpecified; }
        set { isItalicFieldSpecified = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public bool isUnderline
    {
        get { return isUnderlineField; }
        set { isUnderlineField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool isUnderlineSpecified
    {
        get { return isUnderlineFieldSpecified; }
        set { isUnderlineFieldSpecified = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public bool isStrikeThrough
    {
        get { return isStrikeThroughField; }
        set { isStrikeThroughField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool isStrikeThroughSpecified
    {
        get { return isStrikeThroughFieldSpecified; }
        set { isStrikeThroughFieldSpecified = value; }
    }
}