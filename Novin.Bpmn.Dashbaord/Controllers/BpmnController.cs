using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Newtonsoft.Json;
using Novin.Bpmn;
using Novin.Bpmn.Abstractions;
using Novin.Bpmn.Models;

namespace BpmnFileUploader.Controllers
{
    [Authorize]
    public class BpmnController : Controller
    {
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly BpmnEngine _engine;
        private readonly IDefinitionAccessor definitionAccessor;

        public BpmnController(IWebHostEnvironment hostingEnvironment, BpmnEngine engine, IDefinitionAccessor definitionAccessor)
        {
            _hostingEnvironment = hostingEnvironment;
            _engine = engine;
            this.definitionAccessor = definitionAccessor;
        }

        // public IActionResult Index()
        // {
        //     var definitions = definitionAccessor.Get();
        //     return View(definitions);
        // }

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

        // [HttpPost]
        // public IActionResult Delete(string deploymentKey)
        // {
        //     definitionAccessor.Delete(deploymentKey);
        //     return RedirectToAction("Index");
        // }

        public IActionResult Diagram(string fileName)
        {
            ViewBag.FileName = fileName;
            return View();
        }
        //
        // [HttpPost]
        // public IActionResult Save([FromBody] SaveDiagramRequest request)
        // {
        //     // Update the BPMN definition in the storage
        //     definitionAccessor.Update(request.BpmnXML, request.FileName);
        //
        //     return Ok();
        // }


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
}
