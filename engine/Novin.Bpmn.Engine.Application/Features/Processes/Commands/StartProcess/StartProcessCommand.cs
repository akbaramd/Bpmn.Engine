// Application/Commands/StartProcess/StartProcessCommand.cs (add ProjectId + optional ExplicitStartElementId)
using System;
using System.Collections.Generic;
using MediatR;

namespace Novin.Bpmn.Engine.Application.Commands.StartProcess;

public sealed record StartProcessCommand : IRequest<StartProcessResult>
{
    public Guid? ProcessId { get; init; }

    // New instance path
    public Guid ProjectId { get; init; }                // ✅ multi-tenant boundary
    public Guid DeploymentId { get; init; }
    public string? ProcessBpmnId { get; init; }
    public string? ProcessName { get; init; }
    public string? BusinessKey { get; init; }
    public IDictionary<string, string>? InitialVariables { get; init; }

    // Optional: disambiguate multiple start events
    public string? ExplicitStartElementId { get; init; }
}