﻿namespace Novin.Bpmn.Dashbaord.Models;

public class Process
{
    public Guid Id { get; set; }
    public string Content { get; set; }

    public Definitions Definition { get; set; }
    public Guid DefinitionId { get; set; }
}