namespace Novin.Bpmn.Abstractions;

public interface IBpmnDefinitionAccessor
{
    void StoreDefinition( string definitionXml, string deploymentKey);
    NovinBpmnDefinitions GetDefinitionByDeploymentKey(string deploymentKey);
    List<NovinBpmnDefinitions> GetAll(string deploymentKey);
}