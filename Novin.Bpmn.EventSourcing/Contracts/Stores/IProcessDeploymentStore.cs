using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Novin.Bpmn.EventSourcing.Core;
using Novin.Bpmn.Models;

namespace Novin.Bpmn.EventSourcing.Contracts
{

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
            Guid deploymentId,
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
            Guid deploymentKey,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Get raw XML content for a deployment
        /// </summary>
        /// <param name="deploymentKey">Deployment key</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>XML content or null if not found</returns>
        Task<string?> GetRawXmlAsync(
            Guid deploymentKey,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Get parsed BPMN definitions for a deployment
        /// </summary>
        /// <param name="deploymentKey">Deployment key</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>BPMN definitions or null if not found</returns>
        Task<BpmnDefinitions?> GetDefinitionsAsync(
            Guid deplaymentId,
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
