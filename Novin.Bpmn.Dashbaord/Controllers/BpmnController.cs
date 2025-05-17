using Microsoft.AspNetCore.Mvc;
using Novin.Bpmn.Dashbaord.Models;
using Novin.Bpmn.Dashbaord.Services;
using Novin.Bpmn.EventSourcing.Core.Deployments;
using Novin.Bpmn.EventSourcing.Core.Process;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Novin.Bpmn.EventSourcing.Core.Executions;
using Novin.Bpmn.EventSourcing.Core.Services;

namespace Novin.Bpmn.Dashbaord.Controllers
{
    public class BpmnController : Controller
    {
        private readonly BpmnEngineFactory _engineFactory;
        private readonly IWebHostEnvironment _environment;
        private readonly IDeploymentService _deploymentService;
        private readonly IProcessStateStore _processStateStore;
        private readonly IExecutionPathService _executionPathService;
        private readonly IExecutionContextRepository _contextRepository;

        public BpmnController(
            BpmnEngineFactory engineFactory,
            IWebHostEnvironment environment,
            IDeploymentService deploymentService,
            IProcessStateStore processStateStore,
            IExecutionPathService executionPathService,
            IExecutionContextRepository contextRepository)
        {
            _engineFactory = engineFactory;
            _environment = environment;
            _deploymentService = deploymentService;
            _processStateStore = processStateStore;
            _executionPathService = executionPathService;
            _contextRepository = contextRepository;
        }

        public IActionResult Index()
        {
            var deployments = _deploymentService.GetAll();
            return View(deployments);
        }

        public IActionResult ProcessInstances(Guid deploymentKey)
        {
            var instances = _processStateStore.GetByDeploymentKey(deploymentKey);
            return View(new ProcessInstanceListViewModel
            {
                DeploymentKey = deploymentKey,
                Instances = instances.ToList()
            });
        }

        public async Task<IActionResult> Execute(string deploymentKey, string processId)
        {
            try
            {
                var engine = _engineFactory.GetEngine();
                var instance = await engine.StartProcessAsync(deploymentKey, processId,new Dictionary<string, object?>(){
            ["num1"] = 3,
            ["num2"] = 2,
            ["operator"] = "sum"
            });
                return RedirectToAction("ProcessDetail", new { id = instance.InstanceId });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Execution failed: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        [HttpPost]
        public async Task<IActionResult> Upload(IFormFile file, string deploymentKey, string label)
        {
            if (file == null || file.Length == 0 || string.IsNullOrWhiteSpace(deploymentKey))
            {
                TempData["ErrorMessage"] = "File and deployment key are required.";
                return RedirectToAction("Index");
            }

            try
            {
                using var reader = new StreamReader(file.OpenReadStream());
                var bpmnXml = await reader.ReadToEndAsync();
                _deploymentService.Deploy(deploymentKey, bpmnXml);

                TempData["SuccessMessage"] = $"Deployment '{deploymentKey}' uploaded.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Deployment failed: {ex.Message}";
            }

            return RedirectToAction("Index");
        }

        public IActionResult ProcessDetail(Guid id)
        {
            var instance = _processStateStore.Get(id);
            if (instance == null)
                return NotFound();

            var viewModel = new ProcessDetailViewModel
            {
                InstanceId = instance.InstanceId,
                DeploymentKey = instance.DeploymentKey,
                ProcessId = instance.ProcessId,
                Status = instance.Status.ToString(),
                StartTime = instance.CreatedAt,
                EndTime = (instance.Status == ProcessStateStatus.Completed || instance.Status == ProcessStateStatus.Terminated)
                    ? instance.LastUpdatedAt
                    : null,
                Variables = new System.Dynamic.ExpandoObject()
            };

            if (instance.Variables != null)
            {
                var dict = (IDictionary<string, object>)viewModel.Variables;
                foreach (var kvp in instance.Variables)
                    dict[kvp.Key] = kvp.Value;
            }

            return View(viewModel);
        }


      
    }
}

public class ProcessInstanceListViewModel
{
    public Guid DeploymentKey { get; set; }

    public List<ProcessState> Instances { get; set; } = new();

    // می‌توان خواص نمایشی کمکی نیز افزود:
    public string? DeploymentLabel { get; set; } // برای نمایش عنوان در View
}