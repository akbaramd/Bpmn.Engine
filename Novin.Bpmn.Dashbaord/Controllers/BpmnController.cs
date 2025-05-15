using Microsoft.AspNetCore.Mvc;
using Novin.Bpmn.Contracts;
using Novin.Bpmn.Core;
using Novin.Bpmn.Dashbaord.Models;
using Novin.Bpmn.Dashbaord.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Novin.Bpmn.V3;

namespace Novin.Bpmn.Dashbaord.Controllers
{
    public class BpmnController : Controller
    {
        private readonly BpmnEngineFactory _engineFactory;
        private readonly IWebHostEnvironment _environment;

        public BpmnController(BpmnEngineFactory engineFactory, IWebHostEnvironment environment)
        {
            _engineFactory = engineFactory;
            _environment = environment;
        }

        public async Task<IActionResult> Index()
        {
            var engine = _engineFactory.GetEngine();
            var processDefinitions = await engine.GetAllProcessDefinitionsAsync();
            return View(processDefinitions);
        }

        public async Task<IActionResult> Processes(string fileName)
        {
            var engine = _engineFactory.GetEngine();
            var instances = await engine.GetProcessInstancesByDeploymentKeyAsync(fileName);
            
            var model = new ProcessViewModel
            {
                DefinitionKey = fileName,
                Processes = instances.ToList()
            };
            
            return View(model);
        }

        public async Task<IActionResult> Execute(string fileName)
        {
            var engine = _engineFactory.GetEngine();
            var definition = await engine.GetProcessDefinitionAsync(fileName);
            
            if (definition == null)
            {
                return NotFound();
            }
            
            var instance = await engine.StartProcessAsync(definition.DeploymentKey, "process");
            return RedirectToAction("ProcessDetail", new { id = instance.Id });
        }
        
        [HttpPost]
        public async Task<IActionResult> Upload(IFormFile file, string deploymentKey, string label)
        {
            if (file == null || file.Length == 0)
            {
                ModelState.AddModelError("file", "Please select a file to upload.");
                return RedirectToAction("Index");
            }

            if (string.IsNullOrWhiteSpace(deploymentKey))
            {
                ModelState.AddModelError("deploymentKey", "Deployment key is required.");
                return RedirectToAction("Index");
            }

            try
            {
                var engine = _engineFactory.GetEngine();
                
                using (var stream = file.OpenReadStream())
                {
                    // Deploy the process using IBpmnEngine
                    await engine.DeployProcessAsync(deploymentKey, stream, label);
                }
                
                TempData["SuccessMessage"] = $"Process definition '{deploymentKey}' deployed successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deploying process: {ex.Message}";
            }
            
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> ProcessDetail(string id)
        {
            var engine = _engineFactory.GetEngine();
            var instance = await engine.GetProcessInstanceAsync(id);
            
            if (instance == null)
            {
                return NotFound();
            }
            
            // Create view model with process details
            var viewModel = new ProcessDetailViewModel
            {
                Process = new Process 
                { 
                    Id = Guid.Parse(instance.Id),
                    Definition = new Definitions 
                    { 
                        DefinationKey = instance.DeploymentKey 
                    }
                },
                Status = "Active",
                StartTime = DateTime.Now,
                ExecutedNodes = instance.GetExecutedNodes(),
                ExecutedFlows = instance.GetExecutedFlows(),
                ActiveTokens = instance.Tokens.Where(t => t.Status == TokenStatus.Active).ToList(),
                WaitingTokens = instance.Tokens.Where(t => t.Status == TokenStatus.Waiting).ToList(),
                CompletedTokens = instance.Tokens.Where(t => t.Status == TokenStatus.Completed).ToList(),
                Variables = new System.Dynamic.ExpandoObject()
            };
            
            // Copy variables to dynamic object if available
            if (instance.Variables != null)
            {
                var dict = viewModel.Variables as IDictionary<string, object>;
                foreach (var kvp in instance.GetAllVariables())
                {
                    dict[kvp.Key] = kvp.Value;
                }
            }
            
            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> DeleteProcess(string processId)
        {
            var engine = _engineFactory.GetEngine();
            var instance = await engine.GetProcessInstanceAsync(processId);
            
            if (instance == null)
            {
                return NotFound();
            }
            
            await engine.DeleteProcessInstanceAsync(processId);
            return RedirectToAction("Processes", new { fileName = instance.DeploymentKey });
        }
        
        [HttpPost]
        public async Task<IActionResult> DeleteDefinition(string deploymentKey)
        {
            var engine = _engineFactory.GetEngine();
            var result = await engine.DeleteProcessDefinitionAsync(deploymentKey);
            
            if (result)
            {
                TempData["SuccessMessage"] = $"Process definition '{deploymentKey}' deleted successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = $"Failed to delete process definition '{deploymentKey}'.";
            }
            
            return RedirectToAction("Index");
        }
    }
} 