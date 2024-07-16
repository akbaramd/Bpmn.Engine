namespace Novin.Bpmn.Test.Models;

/// <remarks/>
[System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.8.3928.0")]
[Serializable()]
[System.Diagnostics.DebuggerStepThroughAttribute()]
[System.ComponentModel.DesignerCategoryAttribute("code")]
[System.Xml.Serialization.XmlTypeAttribute(Namespace = "http://www.omg.org/spec/DD/20100524/DC")]
[System.Xml.Serialization.XmlRootAttribute(Namespace = "http://www.omg.org/spec/DD/20100524/DC", IsNullable = false)]
public partial class Font
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
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public string name
    {
        get { return nameField; }
        set { nameField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public double size
    {
        get { return sizeField; }
        set { sizeField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlIgnoreAttribute()]
    public bool sizeSpecified
    {
        get { return sizeFieldSpecified; }
        set { sizeFieldSpecified = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public bool isBold
    {
        get { return isBoldField; }
        set { isBoldField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlIgnoreAttribute()]
    public bool isBoldSpecified
    {
        get { return isBoldFieldSpecified; }
        set { isBoldFieldSpecified = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public bool isItalic
    {
        get { return isItalicField; }
        set { isItalicField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlIgnoreAttribute()]
    public bool isItalicSpecified
    {
        get { return isItalicFieldSpecified; }
        set { isItalicFieldSpecified = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public bool isUnderline
    {
        get { return isUnderlineField; }
        set { isUnderlineField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlIgnoreAttribute()]
    public bool isUnderlineSpecified
    {
        get { return isUnderlineFieldSpecified; }
        set { isUnderlineFieldSpecified = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlAttributeAttribute()]
    public bool isStrikeThrough
    {
        get { return isStrikeThroughField; }
        set { isStrikeThroughField = value; }
    }

    /// <remarks/>
    [System.Xml.Serialization.XmlIgnoreAttribute()]
    public bool isStrikeThroughSpecified
    {
        get { return isStrikeThroughFieldSpecified; }
        set { isStrikeThroughFieldSpecified = value; }
    }
}