using System.Collections.Concurrent;
using Novin.Bpmn.Abstractions;

namespace Novin.Bpmn
{
    public class InMemoryBpmnDefinitionAccessor : IBpmnDefinitionAccessor
    {
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, NovinBpmnDefinitions>> storage 
            = new();

        public void StoreDefinition(string definitionXml, string deploymentName)
        {
            var version = "latest";
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

        public NovinBpmnDefinitions GetDefinitionByDeploymentKey(string deploymentKey)
        {
            var version = "latest";
            if (storage.ContainsKey(deploymentKey) && storage[deploymentKey].ContainsKey(version))
            {
                return storage[deploymentKey][version];
            }
            throw new Exception($"Definition with deployment name {deploymentKey} and version {version} not found.");
        }

       

        public List<NovinBpmnDefinitions> GetAll(string deploymentKey)
        {
            return storage.Values.SelectMany(deployment => deployment.Values).Where(x=>x.DeploymentKey.Equals(deploymentKey)).ToList();
        }
    }
}
