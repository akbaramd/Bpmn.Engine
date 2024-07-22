using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using Microsoft.AspNetCore.Authorization;
using System.Text.Json;
using Newtonsoft.Json;
using Novin.Bpmn;

namespace BpmnFileUploader.Controllers
{
    [Authorize]
    public class BpmnController : Controller
    {
        private readonly IWebHostEnvironment _hostingEnvironment;

        public BpmnController(IWebHostEnvironment hostingEnvironment)
        {
            _hostingEnvironment = hostingEnvironment;
        }

        public IActionResult Index()
        {
            var files = Directory.GetFiles(Path.Combine(_hostingEnvironment.WebRootPath, "uploads")).Select(Path.GetFileName).ToList();
            return View(files);
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
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult Delete(string fileName)
        {
            var filePath = Path.Combine(_hostingEnvironment.WebRootPath, "uploads", fileName);

            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }

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
            var filePath = Path.Combine(_hostingEnvironment.WebRootPath, "uploads", request.FileName);

            System.IO.File.WriteAllText(filePath, request.BpmnXML);

            return Ok();
        }

        [HttpGet]
        public IActionResult GetDiagram(string fileName)
        {
            var filePath = Path.Combine(_hostingEnvironment.WebRootPath, "uploads", fileName);

            if (!System.IO.File.Exists(filePath))
            {
                return NotFound();
            }

            var content = System.IO.File.ReadAllText(filePath);
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";
            return Content(content, "text/xml");
        }
        
        public async Task<IActionResult> Execute(string fileName)
        {
            var filePath = Path.Combine(_hostingEnvironment.WebRootPath, "uploads", fileName);
            var engine = new BpmnEngine(filePath);
            await engine.StartProcess();
            ViewBag.Paths  = JsonConvert.SerializeObject(engine.GetExecutedPathsWithFlows());
            ViewBag.FileName = fileName;
            return View(engine.State);
        }
    }

    public class SaveDiagramRequest
    {
        public string FileName { get; set; }
        public string BpmnXML { get; set; }
    }
}
    