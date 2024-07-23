using Newtonsoft.Json;
using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Dashbaord.Data;
using Novin.Bpmn.Dashbaord.Models;

namespace Novin.Bpmn.Dashbaord;

public class EfDefinitionsAccessor : IDefinitionAccessor
{
    private readonly ApplicationDbContext context;

    public EfDefinitionsAccessor(ApplicationDbContext context)
    {
        this.context = context;
    }

    public void Add(string definitionXml, string deploymentName, string version)
    {
        version = (string.IsNullOrWhiteSpace(version)) ? "latest" : version;
        var find = context.Definitions.FirstOrDefault(x =>
            x.DefinationKey.Equals(deploymentName) && x.version.Equals(version));
        if (find is not null)
        {
            throw new Exception("this defination already exists");
        }

        context.Definitions.Add(new Definitions()
        {
            version = version,
            DefinationKey = deploymentName,
            Content = definitionXml,
            CreatedAt = DateTime.Now
        });

        context.SaveChanges();
    }

    public NovinBpmnDefinitions Get(string deploymentName, string version)
    {
        version = (string.IsNullOrWhiteSpace(version)) ? "latest" : version;
        var res = context.Definitions.First(x => x.DefinationKey.Equals(deploymentName) && x.version.Equals(version));

        return new NovinBpmnDefinitions()
        {
            Content = res.Content,
            Version = res.version,
            DeploymentKey = res.DefinationKey,
        };
    }

    public List<NovinBpmnDefinitions> Get(string deploymentName)
    {
        return context.Definitions.Where(x => x.DefinationKey.Equals(deploymentName)).Select(x =>
            new NovinBpmnDefinitions()
            {
                Content = x.Content,
                Version = x.version,
                DeploymentKey = x.DefinationKey
            }).ToList();
    }
}