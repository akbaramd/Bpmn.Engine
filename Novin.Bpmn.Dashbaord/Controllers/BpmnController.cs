using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Novin.Bpmn;
using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Dashbaord.Data;
using Novin.Bpmn.Dashbaord.Models;
using Novin.Bpmn.Models;

namespace BpmnFileUploader.Controllers
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
                _engine.DeployDefinition(filePath, file.FileName);
            }

            return RedirectToAction("Index");
        }
        
        public IActionResult Processes(string fileName)
        {
            var definition = context.Processes.Include(x=>x.Definition).Where(d => d.Definition.DefinationKey == fileName);
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
            context.Definitions.Remove(context.Definitions.First(x => x.DefinationKey.Equals(deploymentKey)));
            context.SaveChanges();
            return RedirectToAction("Index");
        }

        public IActionResult Diagram(string fileName)
        {
            ViewBag.FileName = fileName;
            return View();
        }
        
        [HttpPost]
        public IActionResult Save([FromBody] SaveDiagramRequest request)
        {
            // Update the BPMN definition in the storage
            var defination = context.Definitions.First(x => x.DefinationKey.Equals(request.FileName));
            defination.Content = request.BpmnXML;
            context.Update(defination);
            context.SaveChangesAsync();
            return Ok();
        }


        public async Task<IActionResult> Execute(string fileName)
        {
            var processEngine = await _engine.CreateProcessAsync(fileName);
            await processEngine.StartProcess();
            ViewBag.Paths = JsonConvert.SerializeObject(processEngine.GetExecutedPathsWithFlows());
            ViewBag.FileName = fileName;
            return View(processEngine.ProcessState);
        }
    }

    public class SaveDiagramRequest
    {
        public string FileName { get; set; }
        public string BpmnXML { get; set; }
    }
    public class ProcessViewModel
    {
        public string DefinitionKey { get; set; }
        public List<Process> Processes { get; set; }
    }
}
