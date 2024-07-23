using Novin.Bpmn.Models;

public interface IDefinitionAccessor
 {
     void Add( string definitionXml, string deploymentName, string version);
     NovinBpmnDefinitions Get(string deploymentName, string version);
     List<NovinBpmnDefinitions> Get(string deploymentName);
 }


public class NovinBpmnDefinitions
{
    public string DeploymentKey { get; set; }
    public string Version { get; set; }
    public string Content { get; set; }
}