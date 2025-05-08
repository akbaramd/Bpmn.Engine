using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Novin.Bpmn.Dashbaord.Data;
using Novin.Bpmn.Dashbaord.Models;
using Novin.Bpmn.V3;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Novin.Bpmn.Dashbaord.Controllers
{
    [Authorize]
    public class BpmnController : Controller
    {
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly BpmnEngine _engine;
        private readonly ApplicationDbContext _context;
        private readonly IBpmnV3EngineFactory _v3EngineFactory;

        public BpmnController(
            IWebHostEnvironment hostingEnvironment, 
            BpmnEngine engine, 
            ApplicationDbContext context,
            IBpmnV3EngineFactory v3EngineFactory)
        {
            _hostingEnvironment = hostingEnvironment;
            _engine = engine;
            _context = context;
            _v3EngineFactory = v3EngineFactory;
        }

        public IActionResult Index()
        {
            var definitions = _context.Definitions.ToList();
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
            var definition = _context.Processes.Include(x => x.Definition).Where(d => d.Definition.DefinationKey == fileName);
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
            var definition = _context.Definitions.FirstOrDefault(x => x.DefinationKey.Equals(deploymentKey));
            if (definition != null)
            {
                _context.Definitions.Remove(definition);
                _context.SaveChanges();
            }
            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult DeleteProcess(string processId)
        {
            var process = _context.Processes.FirstOrDefault(x => x.Id.Equals(processId));
            if (process != null)
            {
                _context.Processes.Remove(process);
                _context.SaveChanges();
            }
            return RedirectToAction("Processes", new { fileName = process.Definition.DefinationKey });
        }

        public IActionResult Diagram(string fileName)
        {
            var definition = _context.Definitions.First(x => x.DefinationKey.Equals(fileName));
            return View(definition);
        }

        [HttpPost]
        public IActionResult Save([FromBody] SaveDiagramRequest request)
        {
            // Update the BPMN definition in the storage
            var definition = _context.Definitions.First(x => x.DefinationKey.Equals(request.DefinitionKey));
            definition.Content = request.BpmnXML;
            _context.Definitions.Update(definition);
            _context.SaveChanges();
            return Ok();
        }

        public async Task<IActionResult> Execute(string fileName)
        {
            try
            {
                // دریافت تعریف فرآیند
                var definition = _context.Definitions.First(x => x.DefinationKey.Equals(fileName));
                
                // ایجاد نمونه از موتور V3
                var processInstance = new BpmnV3ProcessInstance("process", definition.Content);
                var processExecutor = new BpmnV3ProcessExecutor(processInstance);
                
                // اجرای فرآیند
                var result = await processExecutor.StartProcessAsync();
                
                // ذخیره نتیجه در دیتابیس
                var process = new Process
                {
                    Id = Guid.NewGuid(),
                    DefinitionId = definition.Id,
                    Content = JsonConvert.SerializeObject(result),
                    Definition = definition
                };
                
                _context.Processes.Add(process);
                _context.SaveChanges();
                
                return RedirectToAction("ProcessDetail", new { id = process.Id });
            }
            catch (Exception ex)
            {
                // مدیریت خطا
                return View("Error", new ErrorViewModel { RequestId = ex.Message });
            }
        }

        public IActionResult ProcessDetail(Guid id)
        {
            var process = _context.Processes.Include(x => x.Definition).First(x => x.Id == id);
            
            try
            {
                // دریافت اطلاعات فرآیند
                var processContent = process.Content;
                
                // تلاش برای تبدیل به نسخه V3
                var v3Instance = JsonConvert.DeserializeObject<BpmnV3ProcessInstance>(processContent);
                
                // ایجاد مدل داده برای نمایش
                var viewModel = new ProcessDetailViewModel
                {
                    Process = process,
                    ExecutedNodes = v3Instance.GetExecutedNodes(),
                    ExecutedFlows = v3Instance.GetExecutedFlows(),
                    ActiveTokens = v3Instance.Tokens.Where(t => t.Status == TokenStatus.Active).ToList(),
                    WaitingTokens = v3Instance.Tokens.Where(t => t.Status == TokenStatus.Waiting).ToList(),
                    CompletedTokens = v3Instance.Tokens.Where(t => t.Status == TokenStatus.Completed).ToList(),
                    Variables = v3Instance.Variables
                };
                
                return View(viewModel);
            }
            catch
            {
                // اگر تبدیل به نسخه V3 ممکن نبود، از روش قبلی استفاده می‌کنیم
                var state = JsonConvert.DeserializeObject<BpmnProcessInstance>(process.Content);
                return View("LegacyProcessDetail", state);
            }
        }
        
        [HttpPost]
        public async Task<IActionResult> CompleteTask(Guid processId, Guid tokenId)
        {
            try
            {
                // دریافت اطلاعات فرآیند
                var process = _context.Processes.Include(x => x.Definition).First(x => x.Id == processId);
                var v3Instance = JsonConvert.DeserializeObject<BpmnV3ProcessInstance>(process.Content);
                
                // ایجاد اجرا کننده فرآیند
                var executor = new BpmnV3ProcessExecutor(v3Instance);
                
                // تکمیل تسک
                var result = await executor.CompleteUserTaskAsync(tokenId);
                
                // به‌روزرسانی اطلاعات فرآیند در دیتابیس
                process.Content = JsonConvert.SerializeObject(result);

                
                _context.Processes.Update(process);
                _context.SaveChanges();
                
                return RedirectToAction("ProcessDetail", new { id = processId });
            }
            catch (Exception ex)
            {
                return View("Error", new ErrorViewModel { RequestId = ex.Message });
            }
        }
        
        [HttpGet]
        public IActionResult ProcessDiagram(Guid id)
        {
            var process = _context.Processes.Include(x => x.Definition).First(x => x.Id == id);
            
            try
            {
                // دریافت اطلاعات فرآیند
                var v3Instance = JsonConvert.DeserializeObject<BpmnV3ProcessInstance>(process.Content);
                
                // ایجاد مدل داده برای نمایش دیاگرام
                var viewModel = new ProcessDiagramViewModel
                {
                    Process = process,
                    BpmnXml = process.Definition.Content,
                    ExecutionMap = new BpmnV3ProcessExecutor(v3Instance).GetExecutionMap(false)
                };
                
                return View(viewModel);
            }
            catch
            {
                // اگر تبدیل به نسخه V3 ممکن نبود، از روش قبلی استفاده می‌کنیم
                var state = JsonConvert.DeserializeObject<BpmnProcessInstance>(process.Content);
                
                var viewModel = new LegacyProcessDiagramViewModel
                {
                    Process = process,
                    BpmnXml = process.Definition.Content,
                    ExecutedPaths = state.GetExecutedPathsWithFlows()
                };
                
                return View("LegacyProcessDiagram", viewModel);
            }
        }

        [HttpGet]
        [Route("api/bpmn/execution-map/{id}")]
        public IActionResult GetExecutionMap(Guid id, bool includeVirtual = true)
        {
            try
            {
                var process = _context.Processes.FirstOrDefault(x => x.Id == id);
                if (process == null)
                {
                    return NotFound();
                }
                
                var v3Instance = JsonConvert.DeserializeObject<BpmnV3ProcessInstance>(process.Content);
                var executor = new BpmnV3ProcessExecutor(v3Instance);
                
                // دریافت نقشه اجرا با پارامتر مناسب
                var executionMap = executor.GetExecutionMap(includeVirtual);
                
                return Json(executionMap);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
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
    
    public class ProcessDiagramViewModel
    {
        public Process Process { get; set; }
        public string BpmnXml { get; set; }
        public ProcessExecutionMap ExecutionMap { get; set; }
    }
    
    public class LegacyProcessDiagramViewModel
    {
        public Process Process { get; set; }
        public string BpmnXml { get; set; }
        public List<BpmnNodeState> ExecutedPaths { get; set; }
    }
    

    
    // این اینترفیس باید در پروژه اصلی تعریف شود
    public interface IBpmnV3EngineFactory
    {
        BpmnV3ProcessExecutor CreateExecutor(string deploymentKey);
        BpmnV3ProcessExecutor CreateExecutorFromInstance(BpmnV3ProcessInstance instance);
    }
}
