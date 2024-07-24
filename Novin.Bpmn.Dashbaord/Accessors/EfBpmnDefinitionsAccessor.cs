using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Dashbaord.Data;
using Novin.Bpmn.Dashbaord.Models;

namespace Novin.Bpmn.Dashbaord.Accessors;

public class EfBpmnDefinitionsAccessor : IBpmnDefinitionAccessor
{
    private readonly ApplicationDbContext context;

    public EfBpmnDefinitionsAccessor(ApplicationDbContext context)
    {
        this.context = context;
    }

    public void StoreDefinition(string definitionXml, string deploymentName)
    {
        var version = "latest";
        var find = context.Definitions.FirstOrDefault(x =>
            x.DefinationKey.Equals(deploymentName) && x.version.Equals(version));
        if (find is not null)
        {
            throw new Exception("this definitions already exists");
        }

        context.Definitions.Add(new Definitions
        {
            version = version,
            DefinationKey = deploymentName,
            Content = definitionXml,
            CreatedAt = DateTime.Now
        });

        context.SaveChanges();
    }

    public NovinBpmnDefinitions GetDefinitionByDeploymentKey(string deploymentKey)
    {
        var version = "latest";
        var res = context.Definitions.First(x => x.DefinationKey.Equals(deploymentKey) && x.version.Equals(version));

        return new NovinBpmnDefinitions
        {
            Content = res.Content,
            Version = res.version,
            DeploymentKey = res.DefinationKey,
        };
    }

    public List<NovinBpmnDefinitions> GetAll(string deploymentKey)
    {
        return context.Definitions.Where(x => x.DefinationKey.Equals(deploymentKey)).Select(x =>
            new NovinBpmnDefinitions
            {
                Content = x.Content,
                Version = x.version,
                DeploymentKey = x.DefinationKey
            }).ToList();
    }
}