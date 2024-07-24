using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Novin.Bpmn.Dashbaord.Data;

namespace Novin.Bpmn.Dashbaord.Controllers.Api;

[Route("/api/bpmn")]
public class BpmnApiController :ControllerBase
{
    private readonly IWebHostEnvironment _hostingEnvironment;
    private readonly BpmnEngine _engine;
    private readonly ApplicationDbContext context;

    public BpmnApiController(IWebHostEnvironment hostingEnvironment, BpmnEngine engine, ApplicationDbContext context)
    {
        _hostingEnvironment = hostingEnvironment;
        _engine = engine;
        this.context = context;
    }
    [HttpGet("content/{deploymentKey}")]
    public string Content(string deploymentKey)
    {
        var defination = context.Definitions.First(x => x.DefinationKey.Equals(deploymentKey));
        return defination.Content;
    }
    
    [HttpGet("content/process/{processId}")]
    public string Content(Guid processId)
    {
        var process = context.Processes.First(x => x.Id.Equals(processId));
        var cInstance = JsonConvert.DeserializeObject<BpmnProcessInstance>(process.Content);
        return cInstance.DefinitionXml;
    }
}