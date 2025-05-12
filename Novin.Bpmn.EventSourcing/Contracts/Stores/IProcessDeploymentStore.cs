using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Novin.Bpmn.EventSourcing.Core;
using Novin.Bpmn.Models;

namespace Novin.Bpmn.EventSourcing.Contracts
{
    /// <summary>
    /// State of a process deployment
    /// </summary>
    public class ProcessDeploymentState
    {
        /// <summary>
        /// Key used to deploy this process definition
        /// </summary>
        public string DeploymentKey { get; set; } = string.Empty;
        
        /// <summary>
        /// Unique ID for this specific definition version
        /// </summary>
        public string DefinitionId { get; set; } = string.Empty;
        
        /// <summary>
        /// Process model ID in the BPMN definition
        /// </summary>
        public string? ProcessId { get; set; }
        
        /// <summary>
        /// Version number of this deployment
        /// </summary>
        public int Version { get; set; }
        
        /// <summary>
        /// Optional display label for this deployment
        /// </summary>
        public string Label { get; set; } = string.Empty;
        
        /// <summary>
        /// Raw XML content of the BPMN definition
        /// </summary>
        public string XmlContent { get; set; } = string.Empty;
        
        /// <summary>
        /// When this definition was deployed
        /// </summary>
        public DateTime DeploymentTime { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Additional metadata about this deployment
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Store for BPMN process definitions
    /// </summary>
    public interface IProcessDeploymentStore
    {
        /// <summary>
        /// Deploy a new BPMN process definition
        /// </summary>
        /// <param name="deploymentKey">Unique key for this deployment</param>
        /// <param name="xmlContent">XML content of the BPMN definition</param>
        /// <param name="definitions">Parsed BPMN definition</param>
        /// <param name="label">Optional display label</param>
        /// <param name="metadata">Additional metadata</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Deployment state</returns>
        Task<ProcessDeploymentState> DeployAsync(
            string deploymentKey,
            string xmlContent,
            BpmnDefinitions definitions,
            string? label = null,
            IDictionary<string, string>? metadata = null,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Get deployment by key
        /// </summary>
        /// <param name="deploymentKey">Deployment key</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Deployment state or null if not found</returns>
        Task<ProcessDeploymentState?> GetDeploymentAsync(
            string deploymentKey,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Get raw XML content for a deployment
        /// </summary>
        /// <param name="deploymentKey">Deployment key</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>XML content or null if not found</returns>
        Task<string?> GetRawXmlAsync(
            string deploymentKey,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Get parsed BPMN definitions for a deployment
        /// </summary>
        /// <param name="deploymentKey">Deployment key</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>BPMN definitions or null if not found</returns>
        Task<BpmnDefinitions?> GetDefinitionsAsync(
            string deploymentKey,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Delete a deployment
        /// </summary>
        /// <param name="deploymentKey">Deployment key</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task DeleteDeploymentAsync(
            string deploymentKey,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// List all deployments
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of deployments</returns>
        Task<IReadOnlyList<ProcessDeploymentState>> ListDeploymentsAsync(
            CancellationToken cancellationToken = default);
    }
}
