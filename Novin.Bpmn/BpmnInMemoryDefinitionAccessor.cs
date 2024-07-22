using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Models;

namespace Novin.Bpmn.Storage
{
    public class BpmnInMemoryDefinitionAccessor : IDefinitionAccessor
    {
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, NovinBpmnDefinitions>> storage 
            = new();

        public void Add(string definitionXml, string deploymentName, string? version = null)
        {
            version ??= "latest";
            if (!storage.ContainsKey(deploymentName))
            {
                storage[deploymentName] = new ConcurrentDictionary<string, NovinBpmnDefinitions>();
            }

            if (storage[deploymentName].ContainsKey(version))
            {
                throw new Exception($"Definition with deployment name {deploymentName} and version {version} already exists.");
            }

            storage[deploymentName][version] = new NovinBpmnDefinitions
            {
                DeploymentKey = deploymentName,
                Version = version,
                Content = definitionXml
            };
        }

        public NovinBpmnDefinitions Get(string deploymentName, string? version = null)
        {
            version ??= "latest";
            if (storage.ContainsKey(deploymentName) && storage[deploymentName].ContainsKey(version))
            {
                return storage[deploymentName][version];
            }
            throw new Exception($"Definition with deployment name {deploymentName} and version {version} not found.");
        }

       

        public List<NovinBpmnDefinitions> Get(string deploymentName)
        {
            return storage.Values.SelectMany(deployment => deployment.Values).Where(x=>x.DeploymentKey.Equals(deploymentName)).ToList();
        }
    }
}
