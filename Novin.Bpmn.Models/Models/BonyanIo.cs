using System;
using System.Collections.Generic;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Serialization;

namespace Novin.Bpmn.Models.Models;

/// <summary>
/// Bonyan IO extension element for input/output mapping with FEEL expressions.
/// </summary>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType("io", Namespace = BpmnXmlNamespaces.Bonyan)]
[XmlRoot("io", Namespace = BpmnXmlNamespaces.Bonyan, IsNullable = false)]
public class BonyanIo
{
    private List<BonyanInput> inputField;
    private List<BonyanOutput> outputField;

    public BonyanIo()
    {
        inputField = new List<BonyanInput>();
        outputField = new List<BonyanOutput>();
    }

    /// <summary>
    /// Input mappings for the activity.
    /// </summary>
    [XmlElement("input", Namespace = BpmnXmlNamespaces.Bonyan)]
    public List<BonyanInput> Input
    {
        get { return inputField; }
        set { inputField = value ?? new List<BonyanInput>(); }
    }

    /// <summary>
    /// Output mappings for the activity.
    /// </summary>
    [XmlElement("output", Namespace = BpmnXmlNamespaces.Bonyan)]
    public List<BonyanOutput> Output
    {
        get { return outputField; }
        set { outputField = value ?? new List<BonyanOutput>(); }
    }
}

/// <summary>
/// Bonyan input mapping with FEEL expression.
/// </summary>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType("input", Namespace = BpmnXmlNamespaces.Bonyan)]
public class BonyanInput
{
    private BonyanFeel feelField;
    private string nameField;

    /// <summary>
    /// The name of the input variable.
    /// </summary>
    [XmlAttribute("name")]
    public string Name
    {
        get { return nameField; }
        set { nameField = value; }
    }

    /// <summary>
    /// The FEEL expression that defines the input value.
    /// </summary>
    [XmlElement("feel", Namespace = BpmnXmlNamespaces.Bonyan)]
    public BonyanFeel Feel
    {
        get { return feelField; }
        set { feelField = value; }
    }
}

/// <summary>
/// Bonyan output mapping with FEEL expression.
/// </summary>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType("output", Namespace = BpmnXmlNamespaces.Bonyan)]
public class BonyanOutput
{
    private BonyanFeel feelField;
    private string targetField;

    /// <summary>
    /// The target for the output (e.g., "process.score", "token.flag", "activity.*").
    /// </summary>
    [XmlAttribute("target")]
    public string Target
    {
        get { return targetField; }
        set { targetField = value; }
    }

    /// <summary>
    /// The FEEL expression that defines the output value.
    /// </summary>
    [XmlElement("feel", Namespace = BpmnXmlNamespaces.Bonyan)]
    public BonyanFeel Feel
    {
        get { return feelField; }
        set { feelField = value; }
    }
}

/// <summary>
/// Bonyan FEEL expression element that can contain CDATA.
/// </summary>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType("feel", Namespace = BpmnXmlNamespaces.Bonyan)]
public class BonyanFeel
{
    private string[] textField;

    /// <summary>
    /// The FEEL expression content (supports CDATA).
    /// </summary>
    [XmlText]
    public string[] Text
    {
        get { return textField; }
        set { textField = value; }
    }

    /// <summary>
    /// Gets the FEEL expression as a single string.
    /// </summary>
    [XmlIgnore]
    public string Expression
    {
        get
        {
            if (textField == null || textField.Length == 0)
                return string.Empty;
            return string.Join(string.Empty, textField);
        }
        set
        {
            textField = string.IsNullOrEmpty(value) ? null : new[] { value };
        }
    }
}

