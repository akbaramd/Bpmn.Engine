using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Elasticsearch.Net;
using Microsoft.Extensions.Logging;
using Nest;
using Newtonsoft.Json;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Core.Models;
using Novin.Bpmn.Models;

namespace Novin.Bpmn.EventSourcing.Core
{
    /// <summary>
    /// Elasticsearch-backed implementation of <see cref="IProcessDeploymentStore"/>.
    /// </summary>
    public class ElasticsearchProcessDeploymentStore : IProcessDeploymentStore
    {
        private const string IndexName = "bpmn-deployments";
        private readonly IElasticClient _elasticClient;
        private readonly ILogger<ElasticsearchProcessDeploymentStore> _logger;
        private readonly JsonSerializerSettings _jsonSettings;

        public ElasticsearchProcessDeploymentStore(
            IElasticClient elasticClient,
            ILogger<ElasticsearchProcessDeploymentStore> logger)
        {
            _elasticClient = elasticClient ?? throw new ArgumentNullException(nameof(elasticClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Use Newtonsoft.Json to serialize definitions with type metadata
            _jsonSettings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                Formatting = Formatting.None
            };

            EnsureIndexExistsAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Ensure the Elasticsearch index with proper mapping exists.
        /// </summary>
        private async Task EnsureIndexExistsAsync()
        {
            var exists = await _elasticClient.Indices.ExistsAsync(IndexName);
            if (exists.Exists) return;

            var create = await _elasticClient.Indices.CreateAsync(IndexName, c => c
                .Settings(s => s
                    .NumberOfShards(1)
                    .NumberOfReplicas(1)
                    .RefreshInterval("1s"))
                .Map(m => m
                    .Properties(ps => ps
                        .Keyword(k => k.Name(nameof(ProcessDeploymentState.DeploymentKey)).IgnoreAbove(256))
                        .Number(n => n.Name(nameof(ProcessDeploymentState.Version)).Type(NumberType.Integer))
                        .Keyword(k => k.Name(nameof(ProcessDeploymentState.Label)).IgnoreAbove(256))
                        .Object<Dictionary<string,string>>(o => o.Name("metadata").Dynamic())
                        .Text(t => t.Name(nameof(ProcessDeploymentState.XmlContent)).Index(false))
                        .Object<object>(o => o.Name("definitions").Dynamic())
                        .Date(d => d.Name(nameof(ProcessDeploymentState.DeploymentTime)))
                    )
                )
            );

            if (!create.IsValid)
            {
                _logger.LogError("Failed to create index '{Index}': {Error}", IndexName, create.DebugInformation);
                throw new ElasticsearchClientException($"Cannot create index {IndexName}: {create.DebugInformation}");
            }
        }

        public async Task<ProcessDeploymentState> DeployAsync(
            string deploymentKey,
            string xmlContent,
            BpmnDefinitions definitions,
            string? label = null,
            IDictionary<string, string>? metadata = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(deploymentKey))
                throw new ArgumentException("deploymentKey is required", nameof(deploymentKey));
            if (string.IsNullOrWhiteSpace(xmlContent))
                throw new ArgumentException("xmlContent is required", nameof(xmlContent));
            if (definitions is null)
                throw new ArgumentNullException(nameof(definitions));

            // Fetch existing to determine next version
            var get = await _elasticClient.GetAsync<Dictionary<string, object>>(deploymentKey, g => g
                .Index(IndexName),
                cancellationToken);

            int nextVersion = 1;
            if (get.IsValid && get.Found && get.Source.TryGetValue("Version", out var v))
            {
                if (int.TryParse(v.ToString(), out var current)) nextVersion = current + 1;
            }

            var now = DateTime.UtcNow;
            var document = new
            {
                DeploymentKey = deploymentKey,
                Version       = nextVersion,
                Label         = label,
                metadata      = metadata,
                XmlContent    = xmlContent,
                definitions   = JsonConvert.SerializeObject(definitions, _jsonSettings),
                DeploymentTime = now
            };

            var indexResp = await _elasticClient.IndexAsync(document, i => i
                .Index(IndexName)
                .Id(deploymentKey)
                .Refresh(Refresh.True),
                cancellationToken);

            if (!indexResp.IsValid)
            {
                _logger.LogError("Error deploying key '{Key}': {Error}", deploymentKey, indexResp.DebugInformation);
                throw new ElasticsearchClientException($"DeployAsync failed: {indexResp.DebugInformation}");
            }

            // Return the stored state
            return new ProcessDeploymentState
            {
                DeploymentId   = Guid.Parse(indexResp.Id),
                DeploymentKey  = deploymentKey,
                Version        = nextVersion,
                Label          = label,
                XmlContent     = xmlContent,
                DeploymentTime = now
            };
        }

        public async Task<ProcessDeploymentState?> GetDeploymentAsync(
            string deploymentKey,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(deploymentKey))
                throw new ArgumentException("deploymentKey is required", nameof(deploymentKey));

            var get = await _elasticClient.GetAsync<Dictionary<string, object>>(deploymentKey, g => g
                .Index(IndexName),
                cancellationToken);

            if (!get.IsValid)
            {
                if (get.ApiCall.HttpStatusCode == 404) return null;
                _logger.LogError("Error fetching deployment '{Key}': {Error}", deploymentKey, get.DebugInformation);
                throw new ElasticsearchClientException($"GetDeploymentAsync failed: {get.DebugInformation}");
            }

            if (!get.Found) return null;

            var src = get.Source;
            return new ProcessDeploymentState
            {
                DeploymentId   = Guid.Parse(deploymentKey),
                DeploymentKey  = src["DeploymentKey"]?.ToString() ?? deploymentKey,
                Version        = Convert.ToInt32(src["Version"]),
                Label          = src["Label"]?.ToString(),
                XmlContent     = src["XmlContent"]?.ToString() ?? string.Empty,
                DeploymentTime = DateTime.Parse(src["DeploymentTime"].ToString()!)
            };
        }

        public async Task<string?> GetRawXmlAsync(
            string deploymentKey,
            CancellationToken cancellationToken = default)
        {
            var d = await GetDeploymentAsync(deploymentKey, cancellationToken);
            return d?.XmlContent;
        }

        public async Task<BpmnDefinitions?> GetDefinitionsAsync(
            string deploymentKey,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(deploymentKey))
                throw new ArgumentException("deploymentKey is required", nameof(deploymentKey));

            var get = await _elasticClient.GetAsync<Dictionary<string, object>>(deploymentKey, g => g
                .Index(IndexName),
                cancellationToken);

            if (!get.IsValid || !get.Found || !get.Source.TryGetValue("definitions", out var raw)) 
                return null;

            try
            {
                return JsonConvert.DeserializeObject<BpmnDefinitions>(
                    raw.ToString()!, _jsonSettings);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deserializing definitions for '{Key}'", deploymentKey);
                throw;
            }
        }

        public async Task DeleteDeploymentAsync(
            string deploymentKey,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(deploymentKey))
                throw new ArgumentException("deploymentKey is required", nameof(deploymentKey));

            var del = await _elasticClient.DeleteAsync(new DeleteRequest(IndexName, deploymentKey), cancellationToken);
            if (!del.IsValid && del.ApiCall.HttpStatusCode != 404)
            {
                _logger.LogError("Error deleting deployment '{Key}': {Error}", deploymentKey, del.DebugInformation);
                throw new ElasticsearchClientException($"DeleteDeploymentAsync failed: {del.DebugInformation}");
            }
        }

        public async Task<IReadOnlyList<ProcessDeploymentState>> ListDeploymentsAsync(
            CancellationToken cancellationToken = default)
        {
            var search = await _elasticClient.SearchAsync<Dictionary<string, object>>(s => s
                .Index(IndexName)
                .Size(1000)
                .Query(q => q.MatchAll()),
                cancellationToken);

            if (!search.IsValid)
                throw new ElasticsearchClientException($"ListDeploymentsAsync failed: {search.DebugInformation}");

            var list = new List<ProcessDeploymentState>();
            foreach (var hit in search.Hits)
            {
                var src = hit.Source;
                list.Add(new ProcessDeploymentState
                {
                    DeploymentId   = Guid.Parse(hit.Id),
                    DeploymentKey  = src["DeploymentKey"]?.ToString() ?? string.Empty,
                    Version        = Convert.ToInt32(src["Version"]),
                    Label          = src["Label"]?.ToString(),
                    XmlContent     = src["XmlContent"]?.ToString() ?? string.Empty,
                    DeploymentTime = DateTime.Parse(src["DeploymentTime"].ToString()!)
                });
            }
            return list;
        }
    }
}
