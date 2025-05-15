using System;
using System.Collections.Generic;
using Novin.Bpmn.Models;
using Novin.Bpmn.Models.Models;

namespace Novin.Bpmn.EventSourcing.Core.Models;

public class BpmnDefinitionDocument
{
    public string DeploymentKey { get; set; } = string.Empty;
    public string DefinitionId { get; set; } = string.Empty;
    public string? ProcessId { get; set; }
    public string XmlContent { get; set; } = string.Empty;
    public DateTime DeploymentTime { get; set; }
    public BpmnDefinitions ParsedDefinition { get; set; } = null!;
    public string Label { get; set; } = string.Empty;
    public int Version { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
    public string Category { get; set; } = "default";
    public List<string> Tags { get; set; } = new();
    public string Description { get; set; } = string.Empty;
} 