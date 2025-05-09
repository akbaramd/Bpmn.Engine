using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Novin.Bpmn.Api.Controllers
{
    [ApiController]
    [Route("api/bpmn")]
    public class BpmnEngineController : ControllerBase
    {
        private readonly BpmnEngine _bpmnEngine;

        public BpmnEngineController(BpmnEngine bpmnEngine)
        {
            _bpmnEngine = bpmnEngine;
        }

        #region Process Definition Management

        /// <summary>
        /// بارگذاری یک فرآیند BPMN جدید
        /// </summary>
        [HttpPost("definitions")]
        public async Task<IActionResult> DeployProcess([FromForm] IFormFile file, [FromForm] string deploymentKey, [FromForm] string label = null)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded");
            }

            if (string.IsNullOrEmpty(deploymentKey))
            {
                deploymentKey = Path.GetFileNameWithoutExtension(file.FileName);
            }

            using (var stream = file.OpenReadStream())
            {
                var definition = await _bpmnEngine.DeployProcessAsync(deploymentKey, stream, label ?? file.FileName);
                return Ok(definition);
            }
        }

        /// <summary>
        /// دریافت لیست تمام تعاریف فرآیندها
        /// </summary>
        [HttpGet("definitions")]
        public async Task<IActionResult> GetAllDefinitions()
        {
            var definitions = await _bpmnEngine.GetAllProcessDefinitionsAsync();
            return Ok(definitions);
        }

        /// <summary>
        /// دریافت یک تعریف فرآیند با کلید
        /// </summary>
        [HttpGet("definitions/{deploymentKey}")]
        public async Task<IActionResult> GetDefinition(string deploymentKey)
        {
            var definition = await _bpmnEngine.GetProcessDefinitionAsync(deploymentKey);
            if (definition == null)
            {
                return NotFound($"Definition with key '{deploymentKey}' not found");
            }

            return Ok(definition);
        }

        /// <summary>
        /// حذف یک تعریف فرآیند
        /// </summary>
        [HttpDelete("definitions/{deploymentKey}")]
        public async Task<IActionResult> DeleteDefinition(string deploymentKey)
        {
            var result = await _bpmnEngine.DeleteProcessDefinitionAsync(deploymentKey);
            if (!result)
            {
                return NotFound($"Definition with key '{deploymentKey}' not found");
            }

            return Ok($"Definition '{deploymentKey}' successfully deleted");
        }

        #endregion

        #region Process Instance Management

        /// <summary>
        /// شروع یک نمونه فرآیند جدید
        /// </summary>
        [HttpPost("instances")]
        public async Task<IActionResult> StartProcess([FromBody] StartProcessRequest request)
        {
            try
            {
                var instanceId = await _bpmnEngine.StartProcessAsync(
                    request.DeploymentKey, 
                    request.ProcessId, 
                    request.Variables);
                
                return Ok(new { InstanceId = instanceId });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error starting process: {ex.Message}");
            }
        }

        /// <summary>
        /// دریافت یک نمونه فرآیند با شناسه
        /// </summary>
        [HttpGet("instances/{instanceId}")]
        public async Task<IActionResult> GetInstance(string instanceId)
        {
            var instance = await _bpmnEngine.GetProcessInstanceAsync(instanceId);
            if (instance == null)
            {
                return NotFound($"Instance with ID '{instanceId}' not found");
            }

            return Ok(instance);
        }

        /// <summary>
        /// دریافت همه نمونه‌های فرآیندهای در حال اجرا
        /// </summary>
        [HttpGet("instances")]
        public async Task<IActionResult> GetAllInstances()
        {
            var instances = await _bpmnEngine.GetAllActiveProcessInstancesAsync();
            return Ok(instances);
        }

        /// <summary>
        /// دریافت همه نمونه‌های یک تعریف فرآیند
        /// </summary>
        [HttpGet("definitions/{deploymentKey}/instances")]
        public async Task<IActionResult> GetInstancesByDefinition(string deploymentKey)
        {
            var instances = await _bpmnEngine.GetProcessInstancesByDeploymentKeyAsync(deploymentKey);
            return Ok(instances);
        }

        /// <summary>
        /// خاتمه دادن به یک نمونه فرآیند
        /// </summary>
        [HttpPost("instances/{instanceId}/terminate")]
        public async Task<IActionResult> TerminateInstance(string instanceId)
        {
            var result = await _bpmnEngine.TerminateProcessInstanceAsync(instanceId);
            if (!result)
            {
                return NotFound($"Instance with ID '{instanceId}' not found");
            }

            return Ok($"Instance '{instanceId}' successfully terminated");
        }

        /// <summary>
        /// حذف یک نمونه فرآیند
        /// </summary>
        [HttpDelete("instances/{instanceId}")]
        public async Task<IActionResult> DeleteInstance(string instanceId)
        {
            var result = await _bpmnEngine.DeleteProcessInstanceAsync(instanceId);
            if (!result)
            {
                return NotFound($"Instance with ID '{instanceId}' not found");
            }

            return Ok($"Instance '{instanceId}' successfully deleted");
        }

        #endregion

        #region Task Management

        /// <summary>
        /// دریافت وظایف کاربری یک کاربر
        /// </summary>
        [HttpGet("tasks/user/{userId}")]
        public IActionResult GetUserTasks(string userId, [FromQuery] string[] groups = null)
        {
            var userGroups = groups != null ? new List<string>(groups) : null;
            var tasks = _bpmnEngine.GetUserTasks(userId, userGroups);
            return Ok(tasks);
        }

        /// <summary>
        /// دریافت وظایف کاربری یک فرآیند
        /// </summary>
        [HttpGet("instances/{instanceId}/tasks")]
        public async Task<IActionResult> GetProcessTasks(string instanceId)
        {
            var tasks = await _bpmnEngine.GetProcessTasksAsync(instanceId);
            return Ok(tasks);
        }

        /// <summary>
        /// تخصیص یک وظیفه به کاربر
        /// </summary>
        [HttpPost("tasks/{tokenId}/claim")]
        public IActionResult ClaimTask(Guid tokenId, [FromBody] ClaimTaskRequest request)
        {
            try
            {
                var task = _bpmnEngine.ClaimTask(tokenId, request.UserId);
                return Ok(task);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// تکمیل یک وظیفه کاربری
        /// </summary>
        [HttpPost("tasks/{tokenId}/complete")]
        public async Task<IActionResult> CompleteTask(Guid tokenId, [FromBody] CompleteTaskRequest request)
        {
            try
            {
                await _bpmnEngine.CompleteTaskAsync(tokenId, request.UserId, request.FormData);
                return Ok("Task completed successfully");
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        #endregion
    }

    public class StartProcessRequest
    {
        public string DeploymentKey { get; set; }
        public string ProcessId { get; set; }
        public Dictionary<string, object> Variables { get; set; }
    }

    public class ClaimTaskRequest
    {
        public string UserId { get; set; }
    }

    public class CompleteTaskRequest
    {
        public string UserId { get; set; }
        public Dictionary<string, object> FormData { get; set; }
    }
} 