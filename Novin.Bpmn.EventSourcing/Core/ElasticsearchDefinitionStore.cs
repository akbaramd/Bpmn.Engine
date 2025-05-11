using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nest;
using Novin.Bpmn.EventSourcing.Contracts;
using Novin.Bpmn.EventSourcing.Core.Models;
using Novin.Bpmn.Models;

namespace Novin.Bpmn.EventSourcing.Core;

/// <summary>
/// Elasticsearch implementation of IDefinitionStore for storing BPMN process definitions
/// </summary>
public class ElasticsearchDefinitionStore : IDefinitionStore
{
    private readonly IElasticClient _elasticClient;
    private readonly ILogger<ElasticsearchDefinitionStore> _logger;
    private const string DefinitionIndexPrefix = "bpmn-definitions-";
    private readonly SemaphoreSlim _indexLock = new(1, 1);

    public ElasticsearchDefinitionStore(
        IElasticClient elasticClient,
        ILogger<ElasticsearchDefinitionStore> logger)
    {
        _elasticClient = elasticClient ?? throw new ArgumentNullException(nameof(elasticClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        EnsureIndexTemplateAsync().GetAwaiter().GetResult();
    }

    private async Task EnsureIndexTemplateAsync()
    {
        try
        {
            await _indexLock.WaitAsync();
            try
            {
                var templateName = DefinitionIndexPrefix + "template";
                var templateExists = await _elasticClient.Indices.TemplateExistsAsync(templateName);
                
                if (!templateExists.Exists)
                {
                    var response = await _elasticClient.Indices.PutTemplateAsync(templateName, t => t
                        .Mappings(m => m
                            .Map<BpmnDefinitionDocument>(tm => tm
                                .Properties(p => p
                                    .Keyword(k => k.Name("deploymentKey"))
                                    .Keyword(k => k.Name("definitionId"))
                                    .Keyword(k => k.Name("processId"))
                                    .Text(t => t.Name("xmlContent"))
                                    .Date(d => d.Name("deploymentTime"))
                                    .Keyword(k => k.Name("label"))
                                    .Object<dynamic>(o => o.Name("parsedDefinition").Dynamic()))))
                        .Settings(s => s
                            .NumberOfShards(1)
                            .NumberOfReplicas(0)
                            .RefreshInterval("1s"))
                        .IndexPatterns(DefinitionIndexPrefix + "*"));

                    if (!response.IsValid)
                    {
                        throw new ElasticsearchException($"Failed to create index template: {response.DebugInformation}");
                    }
                }
            }
            finally
            {
                _indexLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure index template exists");
            throw;
        }
    }

    public async Task<string> SaveDefinitionAsync(
        string deploymentKey,
        string xmlContent,
        BpmnDefinitions definitions,
        string? label = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(deploymentKey))
            throw new ArgumentException("Deployment key cannot be empty", nameof(deploymentKey));
            
        if (string.IsNullOrEmpty(xmlContent))
            throw new ArgumentException("XML content cannot be empty", nameof(xmlContent));
            
        try
        {
            var definitionId = Guid.NewGuid().ToString();
            var indexName = $"{DefinitionIndexPrefix}{DateTime.UtcNow:yyyy-MM}";
            
            var document = new BpmnDefinitionDocument
            {
                DeploymentKey = deploymentKey,
                DefinitionId = definitionId,
                ProcessId = definitions.Items?.OfType<BpmnProcess>().FirstOrDefault()?.id,
                XmlContent = xmlContent,
                DeploymentTime = DateTime.UtcNow,
                Label = label ?? deploymentKey
            };

            var response = await _elasticClient.IndexAsync(document, i => i
                .Index(indexName)
                .Id(definitionId)
                .Refresh(Elasticsearch.Net.Refresh.True),
                cancellationToken);

            if (!response.IsValid)
            {
                throw new ElasticsearchException($"Failed to save definition: {response.DebugInformation}");
            }

            _logger.LogInformation("Saved BPMN definition with key {DeploymentKey} and ID {DefinitionId}", 
                deploymentKey, definitionId);

            return definitionId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving BPMN definition with key {DeploymentKey}", deploymentKey);
            throw new ElasticsearchException($"Failed to save BPMN definition: {ex.Message}", ex);
        }
    }

    public async Task<BpmnDeploymentInfo?> GetDeploymentInfoAsync(
        string deploymentKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(deploymentKey))
            throw new ArgumentException("Deployment key cannot be empty", nameof(deploymentKey));

        try
        {
            var searchResponse = await _elasticClient.SearchAsync<BpmnDefinitionDocument>(s => s
                .Index(DefinitionIndexPrefix + "*")
                .Query(q => q
                    .Match(m => m
                        .Field("deploymentKey")
                        .Query(deploymentKey)))
                .Sort(sort => sort
                    .Descending("deploymentTime"))
                .Size(1),
                cancellationToken);

            if (!searchResponse.IsValid)
            {
                throw new ElasticsearchException($"Failed to search for definition: {searchResponse.DebugInformation}");
            }

            var document = searchResponse.Documents.FirstOrDefault();
            if (document == null)
                return null;

            return new BpmnDeploymentInfo
            {
                DeploymentKey = document.DeploymentKey,
                DefinitionId = document.DefinitionId,
                Label = document.Label,
                XmlContent = document.XmlContent,
                DeploymentTime = document.DeploymentTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving deployment info for key {DeploymentKey}", deploymentKey);
            throw new ElasticsearchException($"Failed to get deployment info: {ex.Message}", ex);
        }
    }

    public async Task<BpmnDefinitions?> GetParsedDefinitionAsync(
        string deploymentKey,
        Func<string, BpmnDefinitions> parseXml,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(deploymentKey))
            throw new ArgumentException("Deployment key cannot be empty", nameof(deploymentKey));

        try
        {
            var searchResponse = await _elasticClient.SearchAsync<BpmnDefinitionDocument>(s => s
                .Index(DefinitionIndexPrefix + "*")
                
                .Sort(sort => sort
                    .Descending("deploymentTime"))
                .Size(1),
                cancellationToken);

            if (!searchResponse.IsValid)
            {
                throw new ElasticsearchException($"Failed to search for definition: {searchResponse.DebugInformation}");
            }

            var document = searchResponse.Documents.FirstOrDefault(x=>x.DeploymentKey == deploymentKey);
            if (document == null)
                return null;

            return  parseXml(document.XmlContent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving parsed definition for key {DeploymentKey}", deploymentKey);
            throw new ElasticsearchException($"Failed to get parsed definition: {ex.Message}", ex);
        }
    }

    public async Task<BpmnDefinitions?> GetParsedDefinitionAsync(
        string deploymentKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(deploymentKey))
            throw new ArgumentException("Deployment key cannot be empty", nameof(deploymentKey));

        try
        {
            var searchResponse = await _elasticClient.SearchAsync<BpmnDefinitionDocument>(s => s
                .Index(DefinitionIndexPrefix + "*")
                .Query(q => q
                    .Term(t => t
                        .Field("deploymentKey.keyword")
                        .Value(deploymentKey)))
                .Sort(sort => sort
                    .Descending("deploymentTime"))
                .Size(1),
                cancellationToken);

            if (!searchResponse.IsValid)
            {
                throw new ElasticsearchException($"Failed to search for definition: {searchResponse.DebugInformation}");
            }

            var document = searchResponse.Documents.FirstOrDefault();
            if (document == null)
                return null;


            // Otherwise parse the XML content
            var serializer = new System.Xml.Serialization.XmlSerializer(typeof(BpmnDefinitions));
            using var reader = new System.IO.StringReader(document.XmlContent);
            var definitions = (BpmnDefinitions)serializer.Deserialize(reader);

            if (definitions == null)
                throw new InvalidOperationException("Failed to deserialize BPMN XML");

            return definitions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving parsed definition for key {DeploymentKey}", deploymentKey);
            throw new ElasticsearchException($"Failed to get parsed definition: {ex.Message}", ex);
        }
    }

    public async Task DeleteDefinitionAsync(
        string deploymentKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(deploymentKey))
            throw new ArgumentException("Deployment key cannot be empty", nameof(deploymentKey));

        try
        {
            var response = await _elasticClient.DeleteByQueryAsync<BpmnDefinitionDocument>(d => d
                .Index(DefinitionIndexPrefix + "*")
                .Query(q => q
                    .Term(t => t
                        .Field("deploymentKey.keyword")
                        .Value(deploymentKey)))
                .Refresh(true),
                cancellationToken);

            if (!response.IsValid)
            {
                throw new ElasticsearchException($"Failed to delete definition: {response.DebugInformation}");
            }

            _logger.LogInformation("Deleted BPMN definition with key {DeploymentKey}", deploymentKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting BPMN definition with key {DeploymentKey}", deploymentKey);
            throw new ElasticsearchException($"Failed to delete BPMN definition: {ex.Message}", ex);
        }
    }

    public async Task<List<BpmnDeploymentInfo>> ListDefinitionsAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var searchResponse = await _elasticClient.SearchAsync<BpmnDefinitionDocument>(s => s
                .Index(DefinitionIndexPrefix + "*")
                .Sort(sort => sort
                    .Descending("deploymentTime"))
                .Size(1000),
                cancellationToken);

            if (!searchResponse.IsValid)
            {
                throw new ElasticsearchException($"Failed to list definitions: {searchResponse.DebugInformation}");
            }

            return searchResponse.Documents
                .Select(d => new BpmnDeploymentInfo
                {
                    DeploymentKey = d.DeploymentKey,
                    DefinitionId = d.DefinitionId,
                    Label = d.Label,
                    XmlContent = d.XmlContent,
                    DeploymentTime = d.DeploymentTime
                })
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing BPMN definitions");
            throw new ElasticsearchException($"Failed to list BPMN definitions: {ex.Message}", ex);
        }
    }
}