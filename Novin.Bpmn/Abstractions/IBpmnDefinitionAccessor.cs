namespace Novin.Bpmn.Abstractions;

public interface IBpmnDefinitionAccessor
{
    void StoreDefinition( string definitionXml, string deploymentKey);
    NovinBpmnDefinitions GetDefinitionByDeploymentKey(string deploymentKey);
    List<NovinBpmnDefinitions> GetAll(string deploymentKey);
}


public class NovinBpmnDefinitions
{
    public string DeploymentKey { get; set; }
    public string Version { get; set; }
    public string Content { get; set; }
}