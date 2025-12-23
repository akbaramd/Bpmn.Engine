using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Serialization;

namespace Novin.Bpmn.Models.Models;

/// <summary>
/// Bonyan Script extension element for script task definitions.
/// Contains script format and script body (code) that will be executed.
/// </summary>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType("script", Namespace = BpmnXmlNamespaces.Bonyan)]
[XmlRoot("script", Namespace = BpmnXmlNamespaces.Bonyan, IsNullable = false)]
public class BonyanScript
{
    private BonyanScriptFormat scriptFormatField;
    private BonyanScriptBody scriptBodyField;

    /// <summary>
    /// Script format/language identifier (e.g., "csharp", "javascript", "python").
    /// Defines the programming language used in the script body.
    /// </summary>
    [XmlElement("scriptFormat", Namespace = BpmnXmlNamespaces.Bonyan)]
    public BonyanScriptFormat ScriptFormat
    {
        get { return scriptFormatField; }
        set { scriptFormatField = value; }
    }

    /// <summary>
    /// Script body containing the actual code to be executed.
    /// Supports CDATA for scripts containing special characters or multi-line content.
    /// </summary>
    [XmlElement("scriptBody", Namespace = BpmnXmlNamespaces.Bonyan)]
    public BonyanScriptBody ScriptBody
    {
        get { return scriptBodyField; }
        set { scriptBodyField = value; }
    }
}

/// <summary>
/// Script format element specifying the programming language.
/// </summary>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType("scriptFormat", Namespace = BpmnXmlNamespaces.Bonyan)]
public class BonyanScriptFormat
{
    private string[] textField;

    /// <summary>
    /// The script format/language as text content.
    /// </summary>
    [XmlText]
    public string[] Text
    {
        get { return textField; }
        set { textField = value; }
    }

    /// <summary>
    /// Gets or sets the script format as a single string.
    /// </summary>
    [XmlIgnore]
    public string Format
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

/// <summary>
/// Script body element containing the actual script code (supports CDATA).
/// </summary>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType("scriptBody", Namespace = BpmnXmlNamespaces.Bonyan)]
public class BonyanScriptBody
{
    private string[] textField;

    /// <summary>
    /// The script code content (supports CDATA).
    /// </summary>
    [XmlText]
    public string[] Text
    {
        get { return textField; }
        set { textField = value; }
    }

    /// <summary>
    /// Gets or sets the script body as a single string.
    /// </summary>
    [XmlIgnore]
    public string Body
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

