using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Novin.Bpmn.EventSourcing.Core.Models;
using Novin.Bpmn.Models;

namespace Novin.Bpmn.EventSourcing.Contracts;

/// <summary>
/// Interface for storing and retrieving BPMN process definitions
/// </summary>
public interface IDefinitionStore
{
    /// <summary>
    /// Saves a BPMN process definition
    /// </summary>
    /// <param name="deploymentKey">Unique key for the deployment</param>
    /// <param name="xmlContent">BPMN XML content</param>
    /// <param name="definitions">Parsed BPMN definitions</param>
    /// <param name="label">Optional label for the deployment</param>
    /// <param name="metadata">Optional metadata for the deployment</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Definition ID</returns>
    Task<string> SaveDefinitionAsync(
        string deploymentKey,
        string xmlContent,
        BpmnDefinitions definitions,
        string? label = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets deployment information for a process definition
    /// </summary>
    /// <param name="deploymentKey">Deployment key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Deployment information or null if not found</returns>
    Task<BpmnDeploymentInfo?> GetDeploymentInfoAsync(
        string deploymentKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the parsed BPMN definitions for a deployment
    /// </summary>
    /// <param name="deploymentKey">Deployment key</param>
    /// <param name="parseXml">Function to parse XML content</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Parsed BPMN definitions or null if not found</returns>
    Task<BpmnDefinitions?> GetParsedDefinitionAsync(
        string deploymentKey,
        Func<string, BpmnDefinitions> parseXml,
        CancellationToken cancellationToken = default);


    Task<BpmnDefinitions?> GetParsedDefinitionAsync(
        string deploymentKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a process definition
    /// </summary>
    /// <param name="deploymentKey">Deployment key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteDefinitionAsync(
        string deploymentKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all deployed process definitions
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of deployment information</returns>
    Task<List<BpmnDeploymentInfo>> ListDefinitionsAsync(
        CancellationToken cancellationToken = default);
} 