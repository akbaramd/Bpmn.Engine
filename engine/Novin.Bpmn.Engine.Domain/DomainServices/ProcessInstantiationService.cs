// Domain/DomainServices/ProcessInstantiationService.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Novin.Bpmn.Engine.Application.Common.Interfaces;
using Novin.Bpmn.Engine.Domain.Entities;

namespace Novin.Bpmn.Engine.Domain.DomainServices;

public interface IProcessInstantiationService
{
    ProcessInstantiationResult Instantiate(ProcessInstantiationRequest request);
}

public sealed record ProcessInstantiationRequest(
    Guid ProjectId,
    Guid DeploymentId,
    string ProcessBpmnId,
    string ProcessName,
    string? BusinessKey = null,
    IDictionary<string, object?>? InitialVariables = null,
    string? ExplicitStartElementId = null
);

public sealed record ProcessInstantiationResult(
    Process Process,
    Token InitialToken,
    string StartElementId
);

public sealed class ProcessInstantiationService : IProcessInstantiationService
{
    private readonly IProjectRepository _projects;
    private readonly IDeploymentRepository _deployments;
    private readonly IBpmnStartResolver _startResolver;

    public ProcessInstantiationService(
        IProjectRepository projects,
        IDeploymentRepository deployments,
        IBpmnStartResolver startResolver)
    {
        _projects = projects ?? throw new ArgumentNullException(nameof(projects));
        _deployments = deployments ?? throw new ArgumentNullException(nameof(deployments));
        _startResolver = startResolver ?? throw new ArgumentNullException(nameof(startResolver));
    }

    public ProcessInstantiationResult Instantiate(ProcessInstantiationRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (request.ProjectId == Guid.Empty) throw new ArgumentException("ProjectId cannot be empty.", nameof(request));
        if (request.DeploymentId == Guid.Empty) throw new ArgumentException("DeploymentId cannot be empty.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.ProcessBpmnId)) throw new ArgumentException("ProcessBpmnId is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.ProcessName)) throw new ArgumentException("ProcessName is required.", nameof(request));

        var project = _projects.GetById(request.ProjectId)
            ?? throw new InvalidOperationException($"Project '{request.ProjectId}' not found.");

        if (!project.IsActive)
            throw new InvalidOperationException("Project is inactive; cannot instantiate process.");

        var deployment = _deployments.GetById(request.DeploymentId)
            ?? throw new InvalidOperationException($"Deployment '{request.DeploymentId}' not found.");

        // Hard invariant (recommended): Deployment MUST belong to Project.
        if (deployment.ProjectId != request.ProjectId)
            throw new InvalidOperationException("Deployment does not belong to the given Project.");

        if (!deployment.IsActive)
            throw new InvalidOperationException("Deployment is inactive; cannot instantiate process.");

        var processBpmnId = request.ProcessBpmnId.Trim();
        var startElementId = ResolveStartElementId(deployment, processBpmnId, request.ExplicitStartElementId);

        // Create + Start (Domain Events emitted by aggregates)
        var process = Process.Create(
            projectId: request.ProjectId,
            deploymentId: request.DeploymentId,
            processBpmnId: processBpmnId,
            name: request.ProcessName.Trim(),
            initialVariables: request.InitialVariables,
            businessKey: request.BusinessKey);

        process.Start();

        var token = new Token(
            processId: process.Id,
            startElementId: startElementId,
            parentTokenId: null);
        token.SetScope(Guid.NewGuid());
        token.Activate();

        // Ownership link (IDs only)
        process.AddToken(token.Id);

        return new ProcessInstantiationResult(process, token, startElementId);
    }

    private string ResolveStartElementId(Deployment deployment, string processBpmnId, string? explicitStartElementId)
    {
        if (!string.IsNullOrWhiteSpace(explicitStartElementId))
        {
            var id = explicitStartElementId.Trim();
            if (!_startResolver.IsValidStartEvent(deployment, processBpmnId, id))
                throw new InvalidOperationException(
                    $"Explicit start element '{id}' is not a valid StartEvent for process '{processBpmnId}'.");
            return id;
        }

        var startIds = _startResolver.GetNoneStartEventIds(deployment, processBpmnId) ?? Array.Empty<string>();
        if (startIds.Count == 0)
            throw new InvalidOperationException($"No NONE StartEvent found for process '{processBpmnId}' in this deployment.");

        if (startIds.Count > 1)
        {
            var list = string.Join(", ", startIds.OrderBy(x => x, StringComparer.Ordinal));
            throw new InvalidOperationException(
                $"Multiple NONE StartEvents found for process '{processBpmnId}'. " +
                $"Pass ExplicitStartElementId to disambiguate. Candidates: {list}");
        }

        return startIds[0];
    }
}
