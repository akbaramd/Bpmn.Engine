using System;
using System.Collections.Generic;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Serialization;

namespace Novin.Bpmn.Models.Models;

/// <summary>
/// Bonyan IO Mapping extension element for input/output variable mapping (Zeebe-style).
/// Maps process variables to node variables (inputs) and node variables to process variables (outputs).
/// </summary>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType("ioMapping", Namespace = BpmnXmlNamespaces.Bonyan)]
[XmlRoot("ioMapping", Namespace = BpmnXmlNamespaces.Bonyan, IsNullable = false)]
public class BonyanIoMapping
{
    private List<BonyanIoMappingInput> inputField;
    private List<BonyanIoMappingOutput> outputField;
    private MissingBehavior onMissingSourceField;
    private MissingBehavior onMissingOutputField;
    private bool overwriteField;

    public BonyanIoMapping()
    {
        inputField = new List<BonyanIoMappingInput>();
        outputField = new List<BonyanIoMappingOutput>();
        onMissingSourceField = MissingBehavior.Skip;
        onMissingOutputField = MissingBehavior.Skip;
        overwriteField = true;
    }

    /// <summary>
    /// Input mappings: Process Variable → Node Variable
    /// Applied when the node is activated.
    /// </summary>
    [XmlElement("input", Namespace = BpmnXmlNamespaces.Bonyan, Type = typeof(BonyanIoMappingInput))]
    public List<BonyanIoMappingInput> Input
    {
        get { return inputField; }
        set { inputField = value ?? new List<BonyanIoMappingInput>(); }
    }

    /// <summary>
    /// Output mappings: Node Variable → Process Variable
    /// Applied when the node is completed.
    /// </summary>
    [XmlElement("output", Namespace = BpmnXmlNamespaces.Bonyan, Type = typeof(BonyanIoMappingOutput))]
    public List<BonyanIoMappingOutput> Output
    {
        get { return outputField; }
        set { outputField = value ?? new List<BonyanIoMappingOutput>(); }
    }

    /// <summary>
    /// Behavior when a source process variable is missing for an input mapping.
    /// Default: Skip (do not set the target node variable).
    /// </summary>
    [XmlAttribute("onMissingSource")]
    [DefaultValue(MissingBehavior.Skip)]
    public MissingBehavior OnMissingSource
    {
        get { return onMissingSourceField; }
        set { onMissingSourceField = value; }
    }

    /// <summary>
    /// Behavior when a source node variable is missing for an output mapping.
    /// Default: Skip (do not update the target process variable).
    /// </summary>
    [XmlAttribute("onMissingOutput")]
    [DefaultValue(MissingBehavior.Skip)]
    public MissingBehavior OnMissingOutput
    {
        get { return onMissingOutputField; }
        set { onMissingOutputField = value; }
    }

    /// <summary>
    /// Whether to overwrite existing process variables when applying outputs.
    /// Default: true (overwrite).
    /// </summary>
    [XmlAttribute("overwrite")]
    [DefaultValue(true)]
    public bool Overwrite
    {
        get { return overwriteField; }
        set { overwriteField = value; }
    }
}

/// <summary>
/// Input mapping: Process Variable → Node Variable
/// The source is a process variable name (may include FEEL expression like "=customerId").
/// The target is the node variable name where the value will be stored.
/// </summary>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType("ioMappingInput", Namespace = BpmnXmlNamespaces.Bonyan)]
public class BonyanIoMappingInput
{
    private string? sourceField;
    private string? targetField;

    /// <summary>
    /// Source process variable name or FEEL expression (e.g., "customerId" or "=customerId").
    /// For simple variable references, the "=" prefix is optional but recommended for FEEL compliance.
    /// </summary>
    [XmlAttribute("source")]
    public string? Source
    {
        get { return sourceField; }
        set { sourceField = value; }
    }

    /// <summary>
    /// Target node variable name where the value will be stored.
    /// This variable will be available in the node's execution context (e.g., script task, service task).
    /// </summary>
    [XmlAttribute("target")]
    public string? Target
    {
        get { return targetField; }
        set { targetField = value; }
    }
}

/// <summary>
/// Output mapping: Node Variable → Process Variable
/// The source is a node variable name (the value produced during node execution).
/// The target is the process variable name where the value will be stored.
/// </summary>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType("ioMappingOutput", Namespace = BpmnXmlNamespaces.Bonyan)]
public class BonyanIoMappingOutput
{
    private string? sourceField;
    private string? targetField;

    /// <summary>
    /// Source node variable name (the value produced during node execution).
    /// </summary>
    [XmlAttribute("source")]
    public string? Source
    {
        get { return sourceField; }
        set { sourceField = value; }
    }

    /// <summary>
    /// Target process variable name where the value will be stored.
    /// This variable will be available to subsequent nodes in the process.
    /// </summary>
    [XmlAttribute("target")]
    public string? Target
    {
        get { return targetField; }
        set { targetField = value; }
    }
}

/// <summary>
/// Behavior policy for handling missing source variables during IO mapping.
/// </summary>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[XmlType(Namespace = BpmnXmlNamespaces.Bonyan)]
public enum MissingBehavior
{
    /// <summary>
    /// Skip the mapping if the source variable is missing (default, safe behavior).
    /// </summary>
    [XmlEnum("skip")]
    Skip,

    /// <summary>
    /// Set the target variable to null if the source variable is missing.
    /// </summary>
    [XmlEnum("null")]
    Null,

    /// <summary>
    /// Fail the node execution if the source variable is missing (strict validation).
    /// </summary>
    [XmlEnum("fail")]
    Fail
}

