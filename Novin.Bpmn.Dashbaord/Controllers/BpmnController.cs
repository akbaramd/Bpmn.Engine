using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Novin.Bpmn.Dashbaord.Data;
using Novin.Bpmn.Dashbaord.Models;

namespace Novin.Bpmn.Dashbaord.Controllers
{
    [Authorize]
    public class BpmnController : Controller
    {
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly BpmnEngine _engine;
        private readonly ApplicationDbContext context;

        public BpmnController(IWebHostEnvironment hostingEnvironment, BpmnEngine engine, ApplicationDbContext context)
        {
            _hostingEnvironment = hostingEnvironment;
            _engine = engine;
            this.context = context;
        }

        public IActionResult Index()
        {
            var definitions = context.Definitions.ToList();
            return View(definitions);
        }

        [HttpPost]
        public IActionResult Upload(IFormFile file)
        {
            if (file != null)
            {
                var filePath = Path.Combine(_hostingEnvironment.WebRootPath, "uploads", file.FileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    file.CopyTo(stream);
                }

                // Deploy the BPMN definition
                _engine.DeployProcessDefinition(filePath, file.FileName);
            }

            return RedirectToAction("Index");
        }

        public IActionResult Processes(string fileName)
        {
            var definition = context.Processes.Include(x => x.Definition).Where(d => d.Definition.DefinationKey == fileName);
            if (definition == null)
            {
                return NotFound();
            }

            var viewModel = new ProcessViewModel
            {
                DefinitionKey = fileName,
                Processes = definition.ToList()
            };

            return View(viewModel);
        }

        [HttpPost]
        public IActionResult Delete(string deploymentKey)
        {
            var definition = context.Definitions.FirstOrDefault(x => x.DefinationKey.Equals(deploymentKey));
            if (definition != null)
            {
                context.Definitions.Remove(definition);
                context.SaveChanges();
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult DeleteProcess(string processId)
        {
            var process = context.Processes.FirstOrDefault(x => x.Id.Equals(processId));
            if (process != null)
            {
                context.Processes.Remove(process);
                context.SaveChanges();
            }
            return RedirectToAction("Processes", new { fileName = process.Definition.DefinationKey });
        }

        public IActionResult Diagram(string fileName)
        {
            var defination = context.Definitions.First(x => x.DefinationKey.Equals(fileName));
            return View(defination);
        }

        [HttpPost]
        public IActionResult Save([FromBody] SaveDiagramRequest request)
        {
            // Update the BPMN definition in the storage
            var defination = context.Definitions.First(x => x.DefinationKey.Equals(request.DefinitionKey));
            defination.Content = request.BpmnXML;
            context.Definitions.Update(defination);
            context.SaveChanges();
            return Ok();
        }

        public async Task<IActionResult> Execute(string fileName)
        {
            var processEngine = await _engine.CreateProcessExecutorAsync(fileName);
            var state = await processEngine.StartProcessAsync();
            
            return RedirectToAction("ProcessDetail", new { id = state.Id });
        }

        public IActionResult ProcessDetail(Guid id)
        {
            var process = context.Processes.Include(x => x.Definition).First(x => x.Id == id);
            var state = JsonConvert.DeserializeObject<BpmnProcessInstance>(process.Content);
            return View(state);
        }
    }

    public class SaveDiagramRequest
    {
        public string DefinitionKey { get; set; }
        public string BpmnXML { get; set; }
    }

    public class ProcessViewModel
    {
        public string DefinitionKey { get; set; }
        public List<Process> Processes { get; set; }
    }
}
